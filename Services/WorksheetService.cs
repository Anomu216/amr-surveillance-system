using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class WorksheetService(
    ApplicationDbContext context,
    IASTConfigurationService astConfigurationService) : IWorksheetService
{
    private const string StaphylococcusAureus = "Staphylococcus aureus";

    // =====================================================
    // Worksheet Retrieval
    // =====================================================

    public async Task<List<Worksheet>> GetAllAsync()
    {
        return await context.Worksheets
            .AsNoTracking()
            .Include(w => w.Patient)
            .Include(w => w.Organism)
            .OrderBy(w => w.IsolateID)
            .ToListAsync();
    }

    public async Task<Worksheet?> GetByIdAsync(
        string isolateID)
    {
        var normalizedId = isolateID.Trim();

        return await context.Worksheets
            .Include(w => w.Patient)
            .Include(w => w.Organism)
            .FirstOrDefaultAsync(w =>
                w.IsolateID == normalizedId);
    }

    public async Task<bool> ExistsAsync(
        string isolateID)
    {
        var normalizedId = isolateID.Trim();

        return await context.Worksheets
            .AsNoTracking()
            .AnyAsync(w =>
                w.IsolateID == normalizedId);
    }

    // =====================================================
    // Reference Validation
    // =====================================================

    public async Task<bool> PatientExistsAsync(
        string patientID)
    {
        var normalizedId = patientID.Trim();

        return await context.Patients
            .AsNoTracking()
            .AnyAsync(p =>
                p.PatientID == normalizedId);
    }

    public async Task<bool> OrganismExistsAsync(
        string whonetID)
    {
        var normalizedId = whonetID.Trim();

        return await context.Organisms
            .AsNoTracking()
            .AnyAsync(o =>
                o.WHONETID == normalizedId);
    }

    // =====================================================
    // Derived MDR / MRSA / Carbapenemase Calculation
    // =====================================================

    public async Task<DerivedWorksheetClassification> CalculateDerivedResultsAsync(
        string? whonetId,
        string? cefoxitinScreen,
        IDictionary<string, string?>? interpretationsByCode)
    {
        var cleanOrg = string.IsNullOrWhiteSpace(whonetId)
            ? null
            : whonetId.Trim();

        bool isStaphAureus = string.Equals(
            cleanOrg,
            StaphylococcusAureus,
            StringComparison.OrdinalIgnoreCase);

        var normalizedInterpretations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (interpretationsByCode != null)
        {
            foreach (var (rawCode, rawSir) in interpretationsByCode)
            {
                if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(rawSir))
                    continue;

                var code = rawCode.Trim().ToUpperInvariant();
                var sir = rawSir.Trim().ToUpperInvariant();

                if (sir is "S" or "I" or "R")
                {
                    normalizedInterpretations[code] = sir;
                }
            }
        }

        bool? mrsa = null;
        if (isStaphAureus)
        {
            var screen = cefoxitinScreen?.Trim().ToUpperInvariant();
            normalizedInterpretations.TryGetValue("OXA", out var oxaSir);

            if (screen is "POS" or "POSITIVE" || oxaSir == "R")
            {
                mrsa = true;
            }
            else if (screen is "NEG" or "NEGATIVE" &&
                     (string.IsNullOrEmpty(oxaSir) || oxaSir == "S"))
            {
                mrsa = false;
            }
        }

        if (cleanOrg == null)
        {
            return new DerivedWorksheetClassification(
                MDR: isStaphAureus && mrsa == true ? true : null,
                MRSA: mrsa,
                Carbapenemase: null);
        }

        var intrinsicCodes = await context.IntrinsicResistances
            .AsNoTracking()
            .Where(r => r.WHONETID.Trim() == cleanOrg)
            .Select(r => r.AntibioticCode.Trim().ToUpperInvariant())
            .ToListAsync();

        var intrinsicCodeSet = new HashSet<string>(
            intrinsicCodes,
            StringComparer.OrdinalIgnoreCase);

        var configuredAntibiotics =
            await astConfigurationService.GetActiveAntibioticsAsync();

        var testedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nonSusceptibleCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        bool anyCarbapenemTested = false;
        bool anyCarbapenemResistant = false;

        foreach (var abx in configuredAntibiotics)
        {
            if (string.IsNullOrWhiteSpace(abx.AntibioticCode) || !abx.InterpretationEnabled)
                continue;

            var code = abx.AntibioticCode.Trim().ToUpperInvariant();

            // Exclude intrinsically resistant antibiotics for this organism
            if (intrinsicCodeSet.Contains(code))
                continue;

            if (!normalizedInterpretations.TryGetValue(code, out var interpretation))
                continue;

            var category = abx.AntibioticClassName?.Trim();

            if (!string.IsNullOrWhiteSpace(category) &&
                category.Contains("Carbapenem", StringComparison.OrdinalIgnoreCase))
            {
                anyCarbapenemTested = true;
                if (interpretation == "R")
                {
                    anyCarbapenemResistant = true;
                }
            }

            if (string.IsNullOrWhiteSpace(category))
                continue;

            testedCategories.Add(category);

            if (interpretation is "I" or "R")
            {
                nonSusceptibleCategories.Add(category);
            }
        }

        bool? carbapenemase = anyCarbapenemTested
            ? anyCarbapenemResistant
            : null;

        bool? mdr = (isStaphAureus && mrsa == true)
            ? true
            : testedCategories.Count < 3
                ? null
                : nonSusceptibleCategories.Count >= 3;

        return new DerivedWorksheetClassification(
            MDR: mdr,
            MRSA: mrsa,
            Carbapenemase: carbapenemase);
    }

    // =====================================================
    // Worksheet Creation
    // =====================================================

    public async Task CreateAsync(
        Worksheet worksheet)
    {
        context.Worksheets.Add(worksheet);
        await context.SaveChangesAsync();
    }

    // =====================================================
    // Worksheet Update
    // =====================================================

    public async Task UpdateAsync(
        string isolateID,
        Worksheet worksheet)
    {
        var normalizedId = isolateID.Trim();

        var existing = await context.Worksheets
            .FirstOrDefaultAsync(w =>
                w.IsolateID == normalizedId);

        if (existing is null)
            return;

        // -------------------------------------------------
        // Worksheet / Isolate Information
        // -------------------------------------------------

        existing.PatientID = worksheet.PatientID;
        existing.CultureResult = worksheet.CultureResult;
        existing.ManuscriptID = worksheet.ManuscriptID;

        // -------------------------------------------------
        // Organism Identification
        // -------------------------------------------------

        existing.AcceptedID = worksheet.AcceptedID;
        existing.WHONETID = worksheet.WHONETID;

        // -------------------------------------------------
        // Laboratory Characteristics
        // -------------------------------------------------

        existing.GramReaction = worksheet.GramReaction;
        existing.Morphology = worksheet.Morphology;
        existing.Hemolysis = worksheet.Hemolysis;
        existing.Catalase = worksheet.Catalase;
        existing.Oxidase = worksheet.Oxidase;
        existing.Coagulase = worksheet.Coagulase;
        existing.LactoseFermentation = worksheet.LactoseFermentation;
        existing.Comments = worksheet.Comments;

        // -------------------------------------------------
        // AST Configuration / Findings
        // -------------------------------------------------

        existing.ASTCard = worksheet.ASTCard;
        existing.ESBL = worksheet.ESBL;
        existing.CefoxitinScreen = worksheet.CefoxitinScreen;
        existing.InducibleClindamycinResistance =
            worksheet.InducibleClindamycinResistance;
        existing.ASTComments = worksheet.ASTComments;

        // -------------------------------------------------
        // Derived Results
        // -------------------------------------------------

        existing.MDR = worksheet.MDR;
        existing.Carbapenemase = worksheet.Carbapenemase;
        existing.MRSA = worksheet.MRSA;

        await context.SaveChangesAsync();
    }

    // =====================================================
    // Worksheet Deletion
    // =====================================================

    public async Task DeleteAsync(
        string isolateID)
    {
        var normalizedId = isolateID.Trim();

        var worksheet = await context.Worksheets
            .FirstOrDefaultAsync(w =>
                w.IsolateID == normalizedId);

        if (worksheet is null)
            return;

        context.Worksheets.Remove(worksheet);
        await context.SaveChangesAsync();
    }
}