using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Imports.Validators;

public class WorksheetImportValidator(ApplicationDbContext context)
{
    private static readonly Regex IsoSuffixRegex = new(
        @"^[A-Z]$",
        RegexOptions.Compiled);

    private static readonly Regex AmrSuffixRegex = new(
        @"^[A-Z0-9]+$",
        RegexOptions.Compiled);

    // =====================================================
    // Validate Worksheet Import Row
    // =====================================================

    public async Task<List<string>> ValidateAsync(
        WorksheetImportRow row,
        HashSet<string> importedIsolateIDs)
    {
        var errors = new List<string>();

        var rawIsolateID = row.IsolateID?.Trim() ?? string.Empty;
        var rawPatientID = row.PatientID?.Trim() ?? string.Empty;
        var rawCultureResult = row.CultureResult?.Trim() ?? string.Empty;

        // =================================================
        // Required Fields
        // =================================================

        if (string.IsNullOrWhiteSpace(rawIsolateID))
        {
            errors.Add("Isolate ID is required.");
        }

        if (string.IsNullOrWhiteSpace(rawPatientID))
        {
            errors.Add("Patient ID is required.");
        }

        if (string.IsNullOrWhiteSpace(rawCultureResult))
        {
            errors.Add("Culture Result is required.");
        }

        var isolateID = rawIsolateID.ToUpperInvariant();
        var patientID = rawPatientID.ToUpperInvariant();

        // =================================================
        // Isolate ID Uniqueness Validation
        // =================================================

        if (!string.IsNullOrWhiteSpace(isolateID))
        {
            if (importedIsolateIDs.Contains(isolateID))
            {
                errors.Add($"Duplicate Isolate ID '{isolateID}' in the import file.");
            }

            var exists = await context.Worksheets
                .AsNoTracking()
                .AnyAsync(w => w.IsolateID == isolateID);

            if (exists)
            {
                errors.Add($"Isolate ID '{isolateID}' already exists.");
            }
        }

        // =================================================
        // Patient Validation
        // =================================================

        if (!string.IsNullOrWhiteSpace(patientID))
        {
            var patientExists = await context.Patients
                .AsNoTracking()
                .AnyAsync(p => p.PatientID == patientID);

            if (!patientExists)
            {
                errors.Add($"Patient ID '{patientID}' does not exist.");
            }
        }

        // =================================================
        // CultureResult & IsolateID Format Rules
        // =================================================

        bool isNoGrowth = IsNoGrowth(rawCultureResult);
        bool isRejected = string.Equals(
            rawCultureResult,
            "Rejected",
            StringComparison.OrdinalIgnoreCase);
        bool isGrowth = string.Equals(
            rawCultureResult,
            "Growth",
            StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(rawCultureResult) &&
            !isNoGrowth &&
            !isGrowth &&
            !isRejected)
        {
            errors.Add($"Invalid Culture Result '{rawCultureResult}'. Must be Growth, No Growth, or Rejected.");
        }
        else if (isNoGrowth)
        {
            if (!string.IsNullOrWhiteSpace(isolateID) &&
                !string.IsNullOrWhiteSpace(patientID) &&
                !string.Equals(isolateID, patientID, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("When Culture Result is No Growth, Isolate ID must match the Patient ID exactly.");
            }
        }
        else if (isGrowth &&
                 !string.IsNullOrWhiteSpace(isolateID) &&
                 !string.IsNullOrWhiteSpace(patientID))
        {
            var expectedPrefix = $"{patientID}-";

            if (!isolateID.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"For Growth cultures, Isolate ID '{isolateID}' must start with '{expectedPrefix}'.");
            }
            else
            {
                var suffix = isolateID[expectedPrefix.Length..];

                if (patientID.StartsWith("ISO", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsoSuffixRegex.IsMatch(suffix))
                    {
                        errors.Add($"When Patient ID starts with ISO, Isolate ID '{isolateID}' must end with a single letter after '{expectedPrefix}'.");
                    }
                }
                else if (patientID.StartsWith("AMR", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AmrSuffixRegex.IsMatch(suffix))
                    {
                        errors.Add($"When Patient ID starts with AMR, Isolate ID '{isolateID}' must end with alphanumeric characters after '{expectedPrefix}'.");
                    }
                }
            }
        }

        // =================================================
        // Organism Validation
        // =================================================

        var whonetID = row.WHONETID?.Trim();

        if (!string.IsNullOrWhiteSpace(whonetID))
        {
            var organismExists = await context.Organisms
                .AsNoTracking()
                .AnyAsync(o => o.WHONETID == whonetID);

            if (!organismExists)
            {
                errors.Add($"WHONET ID '{whonetID}' does not exist.");
            }
        }

        // =================================================
        // Load Database AST Card Configuration
        // =================================================

        var activeCards = await context.ASTCards
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .ToListAsync();

        var dbCardsByCode = activeCards.ToDictionary(
            c => c.ASTCard.Trim().ToUpperInvariant(),
            StringComparer.OrdinalIgnoreCase);

        var legacyCardStrings = await context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active && !string.IsNullOrWhiteSpace(a.ASTCard))
            .Select(a => a.ASTCard)
            .ToListAsync();

        var availableCards = dbCardsByCode.Keys
            .Concat(legacyCardStrings.SelectMany(value =>
                value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(c => c.ToUpperInvariant())))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var selectedCards = string.IsNullOrWhiteSpace(row.ASTCard)
            ? []
            : row.ASTCard
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(c => c.ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        // =================================================
        // AST Card & Organism Compatibility Validation
        // =================================================

        foreach (var card in selectedCards)
        {
            if (!availableCards.Contains(card))
            {
                errors.Add($"AST Card '{card}' is not configured.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(whonetID) &&
                dbCardsByCode.TryGetValue(card, out var cardDef))
            {
                var allowedOrgs = cardDef.ASTCardOrganisms
                    .Select(o => o.WHONETID.Trim())
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                if (allowedOrgs.Count > 0 && !allowedOrgs.Contains(whonetID))
                {
                    errors.Add($"AST Card '{card}' is not mapped to organism '{whonetID}'.");
                }
            }
        }

        // =================================================
        // POS / NEG Findings Validation
        // =================================================

        ValidatePosNeg(row.ESBL, "ESBL", errors);
        ValidatePosNeg(row.CefoxitinScreen, "Cefoxitin Screen", errors);
        ValidatePosNeg(row.InducibleClindamycinResistance, "Inducible Clindamycin Resistance", errors);

        // =================================================
        // Dynamic AST Result Validation
        // =================================================

        await ValidateASTResultsAsync(
            row,
            selectedCards,
            dbCardsByCode,
            errors);

        return errors;
    }

    // =====================================================
    // Validate Dynamic AST Results
    // =====================================================

    private async Task ValidateASTResultsAsync(
        WorksheetImportRow row,
        List<string> selectedCards,
        Dictionary<string, ASTCardDefinition> dbCardsByCode,
        List<string> errors)
    {
        var antibiotics = await context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active)
            .ToListAsync();

        var configuredAntibiotics = antibiotics.ToDictionary(
            a => a.AntibioticCode.Trim(),
            StringComparer.OrdinalIgnoreCase);

        var allowedCodesFromDbCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unconfiguredCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in selectedCards)
        {
            if (dbCardsByCode.TryGetValue(card, out var cardDef) &&
                cardDef.ASTCardAntibiotics.Count > 0)
            {
                foreach (var abx in cardDef.ASTCardAntibiotics)
                {
                    allowedCodesFromDbCards.Add(abx.AntibioticCode.Trim());
                }
            }
            else
            {
                unconfiguredCards.Add(card);
            }
        }

        var micLookup = new Dictionary<string, string?>(
                    row.MICResults,
                    StringComparer.OrdinalIgnoreCase);

        var intLookup = new Dictionary<string, string?>(
            row.InterpretationResults,
            StringComparer.OrdinalIgnoreCase);

        var allCodes = micLookup.Keys
            .Concat(intLookup.Keys)
            .Select(k => k.Trim().ToUpperInvariant())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var code in allCodes)
        {
            bool hasMicEntry = micLookup.TryGetValue(code, out var rawMic) &&
                               !string.IsNullOrWhiteSpace(rawMic);
            bool hasIntEntry = intLookup.TryGetValue(code, out var rawInt) &&
                               !string.IsNullOrWhiteSpace(rawInt);

            if (!hasMicEntry && !hasIntEntry)
            {
                continue;
            }

            if (!configuredAntibiotics.TryGetValue(code, out var antibiotic))
            {
                errors.Add($"Antibiotic '{code}' is not configured.");
                continue;
            }

            if (hasMicEntry && !antibiotic.MICEnabled)
            {
                errors.Add($"MIC is not enabled for antibiotic '{code}'.");
            }

            if (hasIntEntry)
            {
                if (!antibiotic.InterpretationEnabled)
                {
                    errors.Add($"Interpretation is not enabled for antibiotic '{code}'.");
                }

                var interpretation = rawInt!.Trim().ToUpperInvariant();
                if (interpretation is not ("S" or "I" or "R"))
                {
                    errors.Add($"Invalid interpretation '{rawInt}' for antibiotic '{code}'. Use S, I or R.");
                }
            }

            if (hasMicEntry && !hasIntEntry)
            {
                errors.Add($"SIR interpretation is required for '{code}' when MIC ('{rawMic!.Trim()}') is provided.");
            }
            else if (hasIntEntry && antibiotic.MICEnabled && !hasMicEntry)
            {
                errors.Add($"MIC value is required for '{code}' when SIR interpretation ('{rawInt!.Trim()}') is provided.");
            }

            if (selectedCards.Count == 0)
            {
                errors.Add($"Cannot import AST results for '{code}' because no AST Card is specified.");
                continue;
            }

            bool matchesDbCard = allowedCodesFromDbCards.Contains(code);
            bool matchesLegacyCard =
                unconfiguredCards.Count > 0 &&
                !string.IsNullOrWhiteSpace(antibiotic.ASTCard) &&
                antibiotic.ASTCard
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(c => unconfiguredCards.Contains(c));

            if (!matchesDbCard && !matchesLegacyCard)
            {
                errors.Add($"Antibiotic '{code}' does not belong to the selected AST Card(s) ({string.Join("; ", selectedCards)}).");
            }
        }
    }

    // =====================================================
    // POS / NEG & Helpers
    // =====================================================

    private static bool IsNoGrowth(string? cultureResult)
    {
        var val = cultureResult?.Trim();
        return string.Equals(val, "No Growth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "NSG", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "NG48", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidatePosNeg(
        string? value,
        string fieldName,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = value.Trim().ToUpperInvariant();

        if (normalized is not ("POS" or "NEG"))
        {
            errors.Add($"{fieldName} must be POS or NEG.");
        }
    }
}