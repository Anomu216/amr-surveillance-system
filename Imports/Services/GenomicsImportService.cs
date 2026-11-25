
using System.Data;
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Services;

public class GenomicsImportService
    : IImportService<GenomicsRecordViewModel>
{
    private readonly ApplicationDbContext _context;
    private readonly IGeneReferenceSchemaService _schemaService;

    public GenomicsImportService(
        ApplicationDbContext context,
        IGeneReferenceSchemaService schemaService)
    {
        _context = context;
        _schemaService = schemaService;
    }

    public async Task CreateRangeAsync(
        IEnumerable<GenomicsRecordViewModel> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        var records = entities.ToList();

        if (records.Count == 0)
            return;

        // Normalize and validate isolate identifiers.
        foreach (var record in records)
        {
            if (record == null)
            {
                throw new InvalidOperationException(
                    "The import contains an empty record.");
            }

            record.IsolateID =
                record.IsolateID?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(record.IsolateID))
            {
                throw new InvalidOperationException(
                    "Every Genomics record must have an IsolateID.");
            }

            if (record.IsolateID.Length > 20)
            {
                throw new InvalidOperationException(
                    $"IsolateID '{record.IsolateID}' exceeds 20 characters.");
            }

            record.GeneValues ??=
                new Dictionary<string, bool?>(
                    StringComparer.OrdinalIgnoreCase);

            record.GeneValues = new Dictionary<string, bool?>(
                record.GeneValues,
                StringComparer.OrdinalIgnoreCase);

            NormalizeStrings(record);
        }

        // Prevent duplicate isolate IDs within this workbook.
        var duplicateIds = records
            .GroupBy(
                record => record.IsolateID,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new InvalidOperationException(
                "Duplicate isolate IDs were found in the import: " +
                string.Join(", ", duplicateIds));
        }

        var isolateIds = records
            .Select(record => record.IsolateID)
            .ToList();

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            // Retrieve each isolate's WHONETID from Worksheets.
            // This supports both isolate-existence and organism-match checks.
            var worksheetRecords = await _context.Worksheets
                .AsNoTracking()
                .Where(worksheet =>
                    isolateIds.Contains(worksheet.IsolateID))
                .Select(worksheet => new
                {
                    worksheet.IsolateID,
                    worksheet.WHONETID
                })
                .ToListAsync();

            var worksheetByIsolate = worksheetRecords.ToDictionary(
                worksheet => worksheet.IsolateID,
                StringComparer.OrdinalIgnoreCase);

            var missingIds = isolateIds
                .Where(id => !worksheetByIsolate.ContainsKey(id))
                .ToList();

            if (missingIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "These isolates do not exist in Worksheets: " +
                    string.Join(", ", missingIds));
            }

            // Recheck for existing Genomics records at confirmation.
            var existingIds = await _context.Set<Genomics>()
                .AsNoTracking()
                .Where(record =>
                    isolateIds.Contains(record.IsolateID))
                .Select(record => record.IsolateID)
                .ToListAsync();

            if (existingIds.Count > 0)
            {
                throw new InvalidOperationException(
                    "Genomics records already exist for these isolates: " +
                    string.Join(", ", existingIds));
            }

            // Load active registered genes.
            var registeredGenes = await _context.Set<GeneReference>()
                .AsNoTracking()
                .Where(gene => gene.IsActive)
                .Select(gene => gene.GeneName)
                .ToListAsync();

            var registeredGeneSet = new HashSet<string>(
                registeredGenes,
                StringComparer.OrdinalIgnoreCase);

            // Validate gene names, model annotations and WHONETIDs
            // before performing any inserts.
            foreach (var record in records)
            {
                foreach (var geneValue in record.GeneValues)
                {
                    if (string.IsNullOrWhiteSpace(geneValue.Key) ||
                        !registeredGeneSet.Contains(geneValue.Key))
                    {
                        throw new InvalidOperationException(
                            $"Gene '{geneValue.Key}' is not a registered active gene.");
                    }

                    // GeneValues is Dictionary<string, bool?>.
                    // true = detected; false = not detected;
                    // null = blank, unknown, or not tested.
                }

                ValidateModelAnnotations(record);

                var worksheetRecord =
                    worksheetByIsolate[record.IsolateID];

                await ValidateWHONETIDAsync(
                    record.IsolateID,
                    record.WHONETID,
                    worksheetRecord.WHONETID);
            }

            // Ensure all active genes have SQL columns.
            foreach (var geneName in registeredGenes)
            {
                await _schemaService.EnsureGeneColumnAsync(geneName);
            }

            // Create the fixed-field entities.
            var genomicsEntities = records
                .Select(ToEntity)
                .ToList();

            await _context.Set<Genomics>()
                .AddRangeAsync(genomicsEntities);

            await _context.SaveChangesAsync();

            // Save gene values in the same transaction.
            // Omitted genes are explicitly set to SQL NULL.
            foreach (var record in records)
            {
                foreach (var geneName in registeredGenes)
                {
                    record.GeneValues.TryGetValue(
                        geneName,
                        out var geneValue);

                    var sql =
                        $"UPDATE [dbo].[Genomics] " +
                        $"SET {QuoteIdentifier(geneName)} = @geneValue " +
                        "WHERE [IsolateID] = @isolateId;";

                    var parameters = new object[]
                    {
                        new SqlParameter(
                            "@geneValue",
                            SqlDbType.Bit)
                        {
                            Value = geneValue.HasValue
                                ? geneValue.Value
                                : DBNull.Value
                        },
                        new SqlParameter(
                            "@isolateId",
                            record.IsolateID)
                    };

                    var affectedRows =
                        await _context.Database.ExecuteSqlRawAsync(
                            sql,
                            parameters);

                    if (affectedRows == 0)
                    {
                        throw new InvalidOperationException(
                            $"Could not save gene '{geneName}' for isolate '{record.IsolateID}'.");
                    }
                }
            }

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // VALIDATION HELPERS
    // ============================================================

    private async Task ValidateWHONETIDAsync(
        string isolateId,
        string? whonetId,
        string? worksheetWHONETID)
    {
        var normalizedSubmitted = NormalizeOptionalString(whonetId);
        var normalizedWorksheet = NormalizeOptionalString(worksheetWHONETID);

        // The Genomics WHONETID must match the value recorded
        // for the same isolate in Worksheets. Two blank values match.
        if (!string.Equals(
                normalizedSubmitted,
                normalizedWorksheet,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The WHONETID for isolate '{isolateId}' must match the WHONETID recorded in Worksheets.");
        }

        // If a WHONETID is supplied, it must also exist in Organisms.
        if (normalizedSubmitted == null)
            return;

        var exists = await _context.Organisms
            .AsNoTracking()
            .AnyAsync(organism =>
                organism.WHONETID == normalizedSubmitted);

        if (!exists)
        {
            throw new InvalidOperationException(
                $"WHONETID '{normalizedSubmitted}' does not exist in Organisms.");
        }
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static void ValidateModelAnnotations(
        GenomicsRecordViewModel model)
    {
        var validationContext =
            new System.ComponentModel.DataAnnotations.ValidationContext(model);

        var validationResults =
            new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        var isValid =
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                model,
                validationContext,
                validationResults,
                validateAllProperties: true);

        if (!isValid)
        {
            throw new InvalidOperationException(
                "Genomics validation failed for isolate '" +
                model.IsolateID + "': " +
                string.Join(
                    "; ",
                    validationResults.Select(
                        result => result.ErrorMessage)));
        }
    }

    private static void NormalizeStrings(
        GenomicsRecordViewModel model)
    {
        var stringProperties = model.GetType()
            .GetProperties()
            .Where(property =>
                property.CanRead &&
                property.CanWrite &&
                property.PropertyType == typeof(string));

        foreach (var property in stringProperties)
        {
            var value = property.GetValue(model) as string;

            if (value != null)
                property.SetValue(model, value.Trim());
        }
    }

    // ============================================================
    // ENTITY MAPPING
    // ============================================================

    private static Genomics ToEntity(
        GenomicsRecordViewModel model)
    {
        var entity = new Genomics();

        var sourceProperties = typeof(GenomicsRecordViewModel)
            .GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

        var destinationProperties = typeof(Genomics)
            .GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance)
            .Where(property => property.CanWrite)
            .ToDictionary(
                property => property.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceProperty in sourceProperties)
        {
            if (sourceProperty.Name ==
                nameof(GenomicsRecordViewModel.GeneValues))
            {
                continue;
            }

            if (!destinationProperties.TryGetValue(
                    sourceProperty.Name,
                    out var destinationProperty))
            {
                continue;
            }

            if (!destinationProperty.PropertyType.IsAssignableFrom(
                    sourceProperty.PropertyType))
            {
                continue;
            }

            // Do not copy EF navigation properties.
            if (sourceProperty.Name == nameof(Genomics.Worksheet) ||
                sourceProperty.Name == nameof(Genomics.Organism) ||
                sourceProperty.Name == nameof(Genomics.Shipment))
            {
                continue;
            }

            destinationProperty.SetValue(
                entity,
                sourceProperty.GetValue(model));
        }

        return entity;
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            identifier.Length > 100 ||
            identifier.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                "Invalid gene column name.");
        }

        return "[" + identifier.Replace("]", "]]") + "]";
    }
}
