using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels.DataRequests;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class IdentifierValidationService :
    IIdentifierValidationService
{
    private readonly ApplicationDbContext _context;

    // ============================================================
    // IDENTIFIER PATTERNS
    // ============================================================

    private static readonly Regex PatientIdRegex =
        new(
            @"^[A-Z]{3}/[A-Z]{3}/\d{3,4}[A-Z]?$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

    private static readonly Regex IsolateIdRegex =
        new(
            @"^[A-Z]{3}/[A-Z]{3}/\d{3,4}[A-Z]?-[A-Z0-9]{1,4}$",
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public IdentifierValidationService(
        ApplicationDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // VALIDATE IDENTIFIERS
    // ============================================================

    public async Task<List<DataRequestIdentifierValidationViewModel>>
        ValidateIdentifiersAsync(
            string identifierType,
            IEnumerable<string> identifiers,
            CancellationToken cancellationToken = default)
    {
        var results =
            new List<DataRequestIdentifierValidationViewModel>();

        var distinctIdentifiers =
            identifiers
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        foreach (var identifier in distinctIdentifiers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result =
                new DataRequestIdentifierValidationViewModel
                {
                    Identifier = identifier
                };

            switch (identifierType)
            {
                case "PatientID":

                    result.IsPatternValid =
                        PatientIdRegex.IsMatch(identifier);

                    if (!result.IsPatternValid)
                    {
                        result.Message =
                            "Invalid Patient ID format.";

                        results.Add(result);
                        continue;
                    }

                    result.ExistsInDatabase =
                        await _context.Patients
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.PatientID == identifier,
                                cancellationToken);

                    result.CanResolveRelationships =
                        result.ExistsInDatabase;

                    result.Message =
                        result.ExistsInDatabase
                            ? null
                            : "Patient ID has a valid format but was not found in the database.";

                    break;


                case "IsolateID":

                    // A valid IsolateID can either be:
                    //
                    // 1. A normal isolate:
                    //    ABC/DEF/123-WSA
                    //
                    // 2. A PatientID-only placeholder where no
                    //    isolate was obtained.
                    result.IsPatternValid =
                        IsolateIdRegex.IsMatch(identifier) ||
                        PatientIdRegex.IsMatch(identifier);

                    if (!result.IsPatternValid)
                    {
                        result.Message =
                            "Invalid Isolate ID format.";

                        results.Add(result);
                        continue;
                    }

                    result.ExistsInDatabase =
                        await _context.Worksheets
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.IsolateID == identifier,
                                cancellationToken);

                    result.CanResolveRelationships =
                        result.ExistsInDatabase;

                    result.Message =
                        result.ExistsInDatabase
                            ? null
                            : "Isolate ID has a valid format but was not found in the database.";

                    break;


                case "KENID":

                    // Regex validation will be added when the formal
                    // KENID format is finalized.
                    result.IsPatternValid = true;

                    result.ExistsInDatabase =
                        await _context.Shipments
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.KENID == identifier,
                                cancellationToken);

                    result.CanResolveRelationships =
                        result.ExistsInDatabase;

                    result.Message =
                        result.ExistsInDatabase
                            ? null
                            : "KEN ID was not found in the database.";

                    break;


                case "MRSNID":

                    // MRSNID is checked in Shipment first.
                    // A matching Shipment record can provide the KENID
                    // needed to resolve related datasets.
                    result.IsPatternValid = true;

                    result.ExistsInDatabase =
                        await _context.Shipments
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.MRSNID == identifier,
                                cancellationToken);

                    result.CanResolveRelationships =
                        result.ExistsInDatabase;

                    result.Message =
                        result.ExistsInDatabase
                            ? null
                            : "MRSN ID was not found in Shipment.";

                    break;


                default:

                    result.IsPatternValid = false;
                    result.ExistsInDatabase = false;
                    result.CanResolveRelationships = false;

                    result.Message =
                        "Unsupported identifier type.";

                    break;
            }

            results.Add(result);
        }

        return results;
    }
}