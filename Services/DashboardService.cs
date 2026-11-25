using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public partial class DashboardService(ApplicationDbContext context) : IDashboardService
{
    private readonly ApplicationDbContext _context = context;

    // ------------------------------------------------------------
    // SPIKE DETECTION CONSTANTS (ROLLING 92-DAY DATA-DRIVEN UCL)
    // ------------------------------------------------------------

    private const int RollingWindowDays = 92;
    private const int MinimumSpikeSampleSize = 30;
    private const int MaxHistoricalWindows = 8;      // Up to 8 prior 92-day periods (~2 years)
    private const int MinimumHistoricalWindows = 3;  // Require >= 3 valid prior periods (N >= 30)
    private const double ControlLimitSigma = 2.0;    // Mean + 2 SD (Upper Control Limit)

    // ------------------------------------------------------------
    // STATIC READONLY ORDER ARRAYS & COMPILED REGEX
    // ------------------------------------------------------------

    private static readonly string[] SexOrder = ["Male", "Female", "Unknown"];

    private static readonly string[] AgeGroupOrder =
    [
        "< 1 year",
        "1–4 years",
        "5–14 years",
        "15–24 years",
        "25–44 years",
        "45–64 years",
        "65+ years",
        "Unknown"
    ];

    private static readonly string[] MilitaryCivilianOrder = ["Military", "Civilian", "Unknown"];
    private static readonly string[] PatientTypeOrder = ["Inpatient", "Outpatient", "Unknown"];
    private static readonly string[] HaicaiOrder = ["HAI", "CAI", "Unknown"];
    private static readonly string[] CultureResultOrder = ["Positive", "Negative", "Contaminated", "Unknown"];
    private static readonly string[] GenomeCompletenessOrder = ["Complete Genome", "Draft / Incomplete", "Not Recorded"];
    private static readonly string[] IsolateIdColumn = ["[IsolateID]"];
    private static readonly string[] RecruitmentOrder = ["Active", "Passive"];

    [GeneratedRegex(@"^[A-Z0-9_]+$")]
    private static partial Regex AntibioticCodeRegex();

    // ------------------------------------------------------------
    // MAIN DASHBOARD ENTRY POINT
    // ------------------------------------------------------------

    public async Task<DashboardViewModel> GetDashboardAsync(
        DashboardViewModel filters,
        CancellationToken cancellationToken = default)
    {
        filters ??= new DashboardViewModel();

        var normalizedRecruitment = NormalizeRecruitmentFilter(filters.RecruitmentType);

        var model = new DashboardViewModel
        {
            StartDate = filters.StartDate,
            EndDate = filters.EndDate,
            RecruitmentType = normalizedRecruitment,
            GroupByRecruitmentType = filters.GroupByRecruitmentType && string.IsNullOrWhiteSpace(normalizedRecruitment),
            FacilityOwnership = filters.FacilityOwnership,
            FacilityIDs = filters.FacilityIDs is not null ? [.. filters.FacilityIDs] : [],
            OrganismIDs = filters.OrganismIDs is not null ? [.. filters.OrganismIDs] : [],
            ASTOrganismIDs = filters.ASTOrganismIDs is not null
                ? [.. filters.ASTOrganismIDs
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)]
                : [],

            GenomicCharacteristic = filters.GenomicCharacteristic,
            GenomicCharacteristicValue = filters.GenomicCharacteristicValue,
            StrainType = filters.StrainType,
            Phylogroup = filters.Phylogroup,
            Serotype = filters.Serotype,
            SCCmecType = filters.SCCmecType,
            SpaType = filters.SpaType,
            OLocus = filters.OLocus,
            KLocus = filters.KLocus,

            DataUpdatedAt = EastAfricaTime.Now
        };

        await PopulateFilterOptionsAsync(model, cancellationToken);

        // Validate date range.
        if (model.StartDate.HasValue &&
            model.EndDate.HasValue &&
            model.StartDate.Value.Date > model.EndDate.Value.Date)
        {
            model.ErrorMessage = "Start date cannot be later than end date.";
            return model;
        }

        // --------------------------------------------------------
        // 1. BASE PATIENT COHORT & ACCEPTED/REJECTED SPLIT
        // --------------------------------------------------------

        var basePatientsQuery = _context.Patients
            .AsNoTracking()
            .AsQueryable();

        if (model.StartDate.HasValue)
        {
            var startDate = model.StartDate.Value.Date;
            basePatientsQuery = basePatientsQuery
                .Where(p => p.EnrollmentDate >= startDate);
        }

        if (model.EndDate.HasValue)
        {
            var endDateExclusive = model.EndDate.Value.Date.AddDays(1);
            basePatientsQuery = basePatientsQuery
                .Where(p => p.EnrollmentDate < endDateExclusive);
        }

        // Apply RecruitmentType filter (Passive = PatientID starts with "ISO"; Active = otherwise)
        basePatientsQuery = ApplyRecruitmentFilter(basePatientsQuery, model.RecruitmentType);

        // Resolve facility IDs from ownership + explicit selection.
        var effectiveFacilityIds = await ResolveEffectiveFacilityIdsAsync(
            model,
            cancellationToken);

        if (effectiveFacilityIds is not null)
        {
            basePatientsQuery = basePatientsQuery.Where(p =>
                p.FacilityID != null &&
                effectiveFacilityIds.Contains(p.FacilityID));
        }

        var rejectedPatientIds = await basePatientsQuery
            .Where(p => p.AcceptReject != null &&
                        EF.Functions.Collate(p.AcceptReject.Trim(), "SQL_Latin1_General_CP1_CI_AS") == "Rejected")
            .Select(p => p.PatientID)
            .ToListAsync(cancellationToken);

        model.RejectedSamples = rejectedPatientIds.Count;

        // Restrict all downstream epidemiological, isolate, AST, and genomic analytics
        // strictly to accepted samples.
        var patientsQuery = basePatientsQuery
            .Where(p =>
                p.AcceptReject != null &&
                EF.Functions.Collate(p.AcceptReject.Trim(), "SQL_Latin1_General_CP1_CI_AS") == "Accepted");

        var patients = await patientsQuery
            .Select(p => new PatientProjection
            {
                PatientID = p.PatientID,
                Sex = p.Sex,
                Age = p.Age,
                AgeUnit = p.AgeUnit,
                MILCIV = p.MILCIV,
                PatientType = p.PatientType,
                HAICAI = p.HAICAI,
                ClinicalPresentation = p.ClinicalPresentation,
                EnrollmentDate = p.EnrollmentDate
            })
            .ToListAsync(cancellationToken);

        // --------------------------------------------------------
        // 2. WORKSHEETS (ACCEPTED PATIENTS ONLY)
        // --------------------------------------------------------

        var worksheetsQuery = _context.Worksheets
            .AsNoTracking()
            .Where(w => patientsQuery.Any(p => p.PatientID == w.PatientID));

        if (model.OrganismIDs.Count > 0)
        {
            var selectedOrganisms = model.OrganismIDs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();

            worksheetsQuery = worksheetsQuery.Where(w =>
                w.WHONETID != null &&
                selectedOrganisms.Contains(w.WHONETID));
        }

        var worksheets = await worksheetsQuery
            .Select(w => new WorksheetProjection
            {
                IsolateID = w.IsolateID,
                PatientID = w.PatientID,
                WHONETID = w.WHONETID,
                CultureResult = w.CultureResult,
                ESBL = w.ESBL,
                Carbapenemase = w.Carbapenemase,
                MRSA = w.MRSA,
                MDR = w.MDR
            })
            .ToListAsync(cancellationToken);

        // If organism filter is active, restrict patient charts to accepted
        // patients who have at least one matching isolate.
        var chartPatients = patients;
        if (model.OrganismIDs.Count > 0)
        {
            var matchingPatientIds = worksheets
                .Select(w => w.PatientID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            chartPatients =
            [
                .. patients.Where(p => matchingPatientIds.Contains(p.PatientID))
            ];
        }

        // --------------------------------------------------------
        // 3. PATIENT KPI & CHARTS (ACCEPTED SAMPLES ONLY)
        // --------------------------------------------------------

        model.TotalPatients = chartPatients.Count;

        model.PatientsBySex = BuildCategoricalCounts(
            chartPatients.Select(p => NormalizeSex(p.Sex)),
            preferredOrder: SexOrder);

        model.PatientsByAgeGroup = BuildCategoricalCounts(
            chartPatients.Select(p => ClassifyAgeGroup(p.Age, p.AgeUnit)),
            preferredOrder: AgeGroupOrder);

        model.PatientsByMilitaryCivilianStatus = BuildCategoricalCounts(
            chartPatients.Select(p =>
                NormalizeStandardLabel(p.MILCIV, "Military", "Civilian")),
            preferredOrder: MilitaryCivilianOrder);

        model.PatientsByPatientType = BuildCategoricalCounts(
            chartPatients.Select(p =>
                NormalizeStandardLabel(p.PatientType, "Inpatient", "Outpatient")),
            preferredOrder: PatientTypeOrder);

        model.PatientsByHAICAI = BuildCategoricalCounts(
            chartPatients.Select(p => NormalizeHAICAI(p.HAICAI)),
            preferredOrder: HaicaiOrder);

        model.PatientsByClinicalPresentation = BuildTopCategoricalCounts(
            chartPatients.Select(p => CleanLabel(p.ClinicalPresentation)),
            topN: 10);

        // --------------------------------------------------------
        // 4. ISOLATE KPI & CHARTS (ACCEPTED SAMPLES ONLY)
        // --------------------------------------------------------

        model.TotalIsolates = worksheets.Count;

        model.PositiveCultures = worksheets.Count(w =>
            IsPositiveCulture(w.CultureResult));

        model.NegativeCultures = worksheets.Count(w =>
            IsNegativeCulture(w.CultureResult));

        model.MDRIsolates = worksheets.Count(w => w.MDR == true);
        model.IsolatesWithMDRNotRecorded = worksheets.Count(w => w.MDR is null);

        model.IsolatesByOrganism = BuildTopCategoricalCounts(
            worksheets.Select(w =>
                string.IsNullOrWhiteSpace(w.WHONETID)
                    ? "Unspecified"
                    : w.WHONETID.Trim()),
            topN: 10);

        model.CultureResults = BuildCategoricalCounts(
            worksheets.Select(w => NormalizeCultureResult(w.CultureResult)),
            preferredOrder: CultureResultOrder);

        // --------------------------------------------------------
        // 5. DYNAMIC AST / CUMULATIVE ANTIBIOGRAM & TRENDS
        // --------------------------------------------------------

        List<string> isolateIds =
        [
            .. worksheets
                .Select(w => w.IsolateID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        var validAntibiotics = await _context.Antibiotics
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var allASTResults = await LoadDynamicASTResultsByIsolateAsync(
            isolateIds,
            validAntibiotics,
            cancellationToken);

        model.IsolatesWithAST = allASTResults.Count(kvp => kvp.Value.Count > 0);

        // If no AST-specific organisms are chosen, inherit the global
        // OrganismIDs filter whenever one or more global organisms are active.
        if (model.ASTOrganismIDs.Count == 0 &&
            model.OrganismIDs.Count > 0)
        {
            model.ASTOrganismIDs =
            [
                .. model.OrganismIDs
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            ];
        }

        // Conditional MRSA KPI: show when Staphylococcus aureus is selected
        // in either the global Organism filter or the AST Organism filter.
        var isStaphAureusSelected =
            model.OrganismIDs.Any(IsStaphylococcusAureus) ||
            model.ASTOrganismIDs.Any(IsStaphylococcusAureus);

        if (isStaphAureusSelected)
        {
            var staphWorksheets = worksheets
                .Where(w => IsStaphylococcusAureus(w.WHONETID))
                .ToList();

            model.ShowMRSAKpi = true;
            model.MRSAIsolates = staphWorksheets.Count(w => w.MRSA == true);
            model.MRSADenominator = staphWorksheets.Count;
        }

        var patientDatesById = patients
            .GroupBy(p => p.PatientID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().EnrollmentDate,
                StringComparer.OrdinalIgnoreCase);

        if (model.ASTOrganismIDs.Count > 0)
        {
            var selectedAstSet = new HashSet<string>(
                model.ASTOrganismIDs,
                StringComparer.OrdinalIgnoreCase);

            var organismWorksheets = worksheets
                .Where(w =>
                    !string.IsNullOrWhiteSpace(w.WHONETID) &&
                    selectedAstSet.Contains(w.WHONETID.Trim()))
                .ToList();

            List<string> organismIsolateIds =
            [
                .. organismWorksheets
                    .Select(w => w.IsolateID)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            ];

            model.ASTOrganismIsolateCount = organismIsolateIds.Count;
            model.ASTOrganismName = string.Join(", ", model.ASTOrganismIDs);

            var organismASTResults = allASTResults
                .Where(kvp =>
                    organismIsolateIds.Contains(
                        kvp.Key,
                        StringComparer.OrdinalIgnoreCase))
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase);

            var astCohortCount = organismWorksheets.Count;
            if (astCohortCount > 0)
            {
                model.ASTResistanceMarkers = new ResistanceMarkerSummary
                {
                    MDRCount = organismWorksheets.Count(w => w.MDR == true),
                    MDRDenominator = astCohortCount,
                    ESBLCount = organismWorksheets.Count(w =>
                        string.Equals(w.ESBL?.Trim(), "POS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(w.ESBL?.Trim(), "POSITIVE", StringComparison.OrdinalIgnoreCase)),
                    ESBLDenominator = astCohortCount,
                    CarbapenemaseCount = organismWorksheets.Count(w => w.Carbapenemase == true),
                    CarbapenemaseDenominator = astCohortCount,
                    MRSACount = organismWorksheets.Count(w => w.MRSA == true),
                    MRSADenominator = astCohortCount
                };
            }

            var aggregatedAST = BuildAntibiogram(
                organismASTResults,
                validAntibiotics);

            model.Antibiogram = aggregatedAST;

            model.AntibioticTrendObservations = BuildAntibioticTrendObservations(
                organismWorksheets,
                organismASTResults,
                patientDatesById,
                validAntibiotics,
                includeRecruitmentSplit: model.IsGroupedByRecruitmentType);

            if (model.ASTOrganismIsolateCount >= 30)
            {
                model.AntibioticSusceptibility =
                [
                    .. aggregatedAST.Select(a => new DashboardDataPoint
                    {
                        Label = a.AntibioticName,
                        Value = a.SusceptiblePercentage,
                        SecondaryValue = a.IntermediatePercentage,
                        TertiaryValue = a.ResistantPercentage,
                        Denominator = a.Tested
                    })
                ];
            }
            else
            {
                model.AntibioticSusceptibility =
                [
                    .. aggregatedAST.Select(a => new DashboardDataPoint
                    {
                        Label = a.AntibioticName,
                        Value = a.Susceptible,
                        SecondaryValue = a.Intermediate,
                        TertiaryValue = a.Resistant,
                        Denominator = a.Tested
                    })
                ];
            }
        }
        else
        {
            // Always populate longitudinal resistance trends across all accepted isolates
            // matching the active global filters when no specific AST organism is selected.
            model.AntibioticTrendObservations = BuildAntibioticTrendObservations(
                worksheets,
                allASTResults,
                patientDatesById,
                validAntibiotics,
                includeRecruitmentSplit: model.IsGroupedByRecruitmentType);
        }

        // --------------------------------------------------------
        // 6. ROLLING 92-DAY DATA-DRIVEN SPIKE DETECTION
        // --------------------------------------------------------

        model.DetectedSpikes = await DetectRollingQuarterlySpikesAsync(
            effectiveFacilityIds,
            model.OrganismIDs,
            model.ASTOrganismIDs,
            validAntibiotics,
            model.RecruitmentType,
            cancellationToken);

        // --------------------------------------------------------
        // 7. GENOMICS & DYNAMIC GENE COLUMNS (ACCEPTED SAMPLES)
        // --------------------------------------------------------

        var genomicsQuery = _context.Genomics
            .AsNoTracking()
            .Where(g =>
                g.IsolateID != null &&
                isolateIds.Contains(g.IsolateID));

        genomicsQuery = ApplyGenomicFilters(genomicsQuery, model);

        var genomics = await genomicsQuery.ToListAsync(cancellationToken);

        model.SequencedIsolates = genomics.Count;

        List<string> sequencedIsolateIds =
        [
            .. genomics
                .Select(g => g.IsolateID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];

        var geneResultsByIsolate = await LoadDynamicGeneResultsAsync(
            sequencedIsolateIds,
            cancellationToken);

        model.IsolatesWithGeneResults = geneResultsByIsolate.Count(kvp => kvp.Value.Count > 0);

        var isolateToPatient = worksheets
            .Where(w => !string.IsNullOrWhiteSpace(w.IsolateID))
            .GroupBy(w => w.IsolateID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().PatientID,
                StringComparer.OrdinalIgnoreCase);

        model.SequencingOverTime = BuildSequencingOverTime(
            genomics,
            isolateToPatient,
            patientDatesById);

        model.GenomeCompleteness = BuildGenomeCompleteness(genomics);

        model.StrainTypesData = BuildTopCategoricalCounts(
            genomics.Select(g => CleanLabel(g.StrainType)),
            topN: 10);

        model.GenePrevalence = BuildGenePrevalence(
            geneResultsByIsolate,
            genomics.Count,
            topN: 15);

        model.GenomicScores = BuildGenomicScores(genomics);

        // --------------------------------------------------------
        // 8. OPTIONAL STRATIFIED BREAKDOWN (WHEN GROUP BY IS OPTED IN)
        // --------------------------------------------------------

        if (model.IsGroupedByRecruitmentType)
        {
            model.RecruitmentStratifiedKpis = BuildRecruitmentStratifiedKpis(
                chartPatients,
                rejectedPatientIds,
                worksheets,
                genomics,
                isolateToPatient,
                isStaphAureusSelected);
        }

        return model;
    }

    // ------------------------------------------------------------
    // ROLLING 92-DAY DATA-DRIVEN SPIKE DETECTION
    // (Independent of UI StartDate/EndDate so historical baseline is preserved)
    // ------------------------------------------------------------

    public async Task<List<ResistanceSpikeAlert>> DetectRollingQuarterlySpikesAsync(
        List<string>? effectiveFacilityIds,
        List<string> globalOrganismIds,
        List<string> astOrganismIds,
        List<Antibiotic> validAntibiotics,
        string? recruitmentType = null,
        CancellationToken cancellationToken = default)
    {
        var today = EastAfricaTime.Now.Date;
        var totalLookbackDays = RollingWindowDays * (MaxHistoricalWindows + 1);
        var earliestDate = today.AddDays(-totalLookbackDays);

        var acceptedPatientsQuery = _context.Patients
            .AsNoTracking()
            .Where(p =>
                p.EnrollmentDate >= earliestDate &&
                p.EnrollmentDate <= today &&
                p.AcceptReject != null &&
                EF.Functions.Collate(p.AcceptReject.Trim(), "SQL_Latin1_General_CP1_CI_AS") == "Accepted");

        acceptedPatientsQuery = ApplyRecruitmentFilter(acceptedPatientsQuery, recruitmentType);

        if (effectiveFacilityIds is not null)
        {
            acceptedPatientsQuery = acceptedPatientsQuery.Where(p =>
                p.FacilityID != null &&
                effectiveFacilityIds.Contains(p.FacilityID));
        }

        var patientDates = await acceptedPatientsQuery
            .Select(p => new
            {
                p.PatientID,
                p.EnrollmentDate
            })
            .ToListAsync(cancellationToken);

        if (patientDates.Count == 0)
        {
            return [];
        }

        var patientDateMap = patientDates
            .Where(p => p.EnrollmentDate.HasValue)
            .GroupBy(p => p.PatientID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().EnrollmentDate!.Value.Date,
                StringComparer.OrdinalIgnoreCase);

        if (patientDateMap.Count == 0)
        {
            return [];
        }

        var historicalWorksheets = await _context.Worksheets
            .AsNoTracking()
            .Where(w => acceptedPatientsQuery.Any(p => p.PatientID == w.PatientID))
            .Select(w => new WorksheetProjection
            {
                IsolateID = w.IsolateID,
                PatientID = w.PatientID,
                WHONETID = w.WHONETID,
                MDR = w.MDR,
                MRSA = w.MRSA
            })
            .ToListAsync(cancellationToken);

        var timestampedIsolates = historicalWorksheets
            .Where(w => !string.IsNullOrWhiteSpace(w.PatientID) &&
                        patientDateMap.ContainsKey(w.PatientID))
            .Select(w => new TimestampedIsolate
            {
                IsolateID = w.IsolateID,
                EnrollmentDate = patientDateMap[w.PatientID],
                WHONETID = w.WHONETID?.Trim() ?? string.Empty,
                MDR = w.MDR,
                MRSA = w.MRSA
            })
            .ToList();

        if (timestampedIsolates.Count < MinimumSpikeSampleSize)
        {
            return [];
        }

        List<ResistanceSpikeAlert> alerts = [];

        // 1. Cohort-wide MDR spike check (scoped to globalOrganismIds if active)
        List<TimestampedIsolate> mdrScopeIsolates = globalOrganismIds.Count > 0
            ? [.. timestampedIsolates.Where(i => globalOrganismIds.Contains(i.WHONETID, StringComparer.OrdinalIgnoreCase))]
            : timestampedIsolates;

        var mdrScopeLabel = globalOrganismIds.Count > 0
            ? string.Join(", ", globalOrganismIds)
            : "All Accepted Isolates";

        EvaluateRollingWindowControlLimit(
            alerts,
            metricType: "MDR",
            metricCode: "MDR",
            metricName: "Multidrug Resistance (MDR)",
            organismLabel: mdrScopeLabel,
            today: today,
            windowStatsProvider: (start, end) =>
            {
                var windowItems = mdrScopeIsolates
                    .Where(i => i.EnrollmentDate >= start && i.EnrollmentDate <= end && i.MDR.HasValue)
                    .ToList();
                return (windowItems.Count(i => i.MDR == true), windowItems.Count);
            });

        // 2. MRSA spike check among Staphylococcus aureus isolates
        var staphIsolates = timestampedIsolates
            .Where(i => IsStaphylococcusAureus(i.WHONETID))
            .ToList();

        if (staphIsolates.Count >= MinimumSpikeSampleSize)
        {
            EvaluateRollingWindowControlLimit(
                alerts,
                metricType: "MRSA",
                metricCode: "MRSA",
                metricName: "Methicillin Resistance (MRSA)",
                organismLabel: "Staphylococcus aureus",
                today: today,
                windowStatsProvider: (start, end) =>
                {
                    var windowItems = staphIsolates
                        .Where(i => i.EnrollmentDate >= start && i.EnrollmentDate <= end && i.MRSA.HasValue)
                        .ToList();
                    return (windowItems.Count(i => i.MRSA == true), windowItems.Count);
                });
        }

        // 3. Per-antibiotic resistance spike check evaluated per target organism
        var targetOrganisms = astOrganismIds.Count > 0
            ? astOrganismIds
            : globalOrganismIds;

        if (targetOrganisms.Count > 0)
        {
            var targetSet = new HashSet<string>(targetOrganisms, StringComparer.OrdinalIgnoreCase);
            List<TimestampedIsolate> targetIsolates =
            [
                .. timestampedIsolates.Where(i => targetSet.Contains(i.WHONETID))
            ];

            if (targetIsolates.Count >= MinimumSpikeSampleSize)
            {
                List<string> targetIds =
                [
                    .. targetIsolates
                        .Select(i => i.IsolateID)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                ];

                var astMap = await LoadDynamicASTResultsByIsolateAsync(
                    targetIds,
                    validAntibiotics,
                    cancellationToken);

                var antibioticLookup = validAntibiotics
                    .Where(a => !string.IsNullOrWhiteSpace(a.AntibioticCode))
                    .GroupBy(a => a.AntibioticCode.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => string.IsNullOrWhiteSpace(g.First().AntibioticName)
                            ? g.Key
                            : g.First().AntibioticName,
                        StringComparer.OrdinalIgnoreCase);

                var isolatesByOrganism = targetIsolates
                    .GroupBy(i => i.WHONETID, StringComparer.OrdinalIgnoreCase);

                foreach (var orgGroup in isolatesByOrganism)
                {
                    List<TimestampedIsolate> orgIsolates = [.. orgGroup];
                    if (orgIsolates.Count < MinimumSpikeSampleSize)
                    {
                        continue;
                    }

                    var organismLabel = orgGroup.First().WHONETID;

                    List<string> testedCodes =
                    [
                        .. orgIsolates
                            .Where(iso => astMap.ContainsKey(iso.IsolateID))
                            .SelectMany(iso => astMap[iso.IsolateID].Keys)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                    ];

                    foreach (var code in testedCodes)
                    {
                        var drugName = antibioticLookup.GetValueOrDefault(code, code);

                        EvaluateRollingWindowControlLimit(
                            alerts,
                            metricType: "Antibiotic",
                            metricCode: code,
                            metricName: $"{drugName} Resistance",
                            organismLabel: organismLabel,
                            today: today,
                            windowStatsProvider: (start, end) =>
                            {
                                var tested = 0;
                                var resistant = 0;

                                foreach (var iso in orgIsolates)
                                {
                                    if (iso.EnrollmentDate < start || iso.EnrollmentDate > end)
                                    {
                                        continue;
                                    }

                                    if (astMap.TryGetValue(iso.IsolateID, out var results) &&
                                        results.TryGetValue(code, out var interpretation))
                                    {
                                        tested++;
                                        if (interpretation == "R")
                                        {
                                            resistant++;
                                        }
                                    }
                                }

                                return (resistant, tested);
                            });
                    }
                }
            }
        }

        return
        [
            .. alerts
                .OrderByDescending(a => a.ZScore)
                .ThenByDescending(a => a.DeltaFromMean)
        ];
    }

    public async Task<List<ResistanceSpikeAlert>> DetectSystemWideRollingSpikesAsync(
        CancellationToken cancellationToken = default)
    {
        var validAntibiotics = await _context.Antibiotics
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        List<string> distinctOrganisms =
        [
            .. await _context.Worksheets
                .AsNoTracking()
                .Where(w => !string.IsNullOrWhiteSpace(w.WHONETID))
                .Select(w => w.WHONETID!.Trim())
                .Distinct()
                .ToListAsync(cancellationToken)
        ];

        return await DetectRollingQuarterlySpikesAsync(
            effectiveFacilityIds: null,
            globalOrganismIds: [],
            astOrganismIds: distinctOrganisms,
            validAntibiotics: validAntibiotics,
            recruitmentType: null,
            cancellationToken: cancellationToken);
    }

    private static void EvaluateRollingWindowControlLimit(
        List<ResistanceSpikeAlert> target,
        string metricType,
        string metricCode,
        string metricName,
        string organismLabel,
        DateTime today,
        Func<DateTime, DateTime, (int Numerator, int Denominator)> windowStatsProvider)
    {
        // Current dynamic rolling window: [today - 91 days, today] (92 inclusive days)
        var currentStart = today.AddDays(-(RollingWindowDays - 1));
        var (currentNum, currentDen) = windowStatsProvider(currentStart, today);

        if (currentDen < MinimumSpikeSampleSize)
        {
            return;
        }

        // Collect preceding non-overlapping 92-day baseline blocks with N >= 30
        List<(double Rate, int Denominator)> validBaselineWindows = [];

        for (var windowIndex = 1; windowIndex <= MaxHistoricalWindows; windowIndex++)
        {
            var periodEnd = currentStart.AddDays(-1 - ((windowIndex - 1) * RollingWindowDays));
            var periodStart = periodEnd.AddDays(-(RollingWindowDays - 1));

            var (num, den) = windowStatsProvider(periodStart, periodEnd);
            if (den >= MinimumSpikeSampleSize)
            {
                validBaselineWindows.Add(((double)num / den * 100.0, den));
            }
        }

        if (validBaselineWindows.Count < MinimumHistoricalWindows)
        {
            return;
        }

        var currentPct = (double)currentNum / currentDen * 100.0;
        var mean = validBaselineWindows.Average(p => p.Rate);

        var variance = validBaselineWindows
            .Sum(p => Math.Pow(p.Rate - mean, 2)) / (validBaselineWindows.Count - 1);
        var stdDev = Math.Sqrt(variance);

        // If historical variance is zero (e.g., 0% across all prior windows), use binomial
        // sampling standard error so a statistically real emergence above baseline is caught.
        var effectiveSigma = stdDev > 0
            ? stdDev
            : Math.Sqrt(Math.Max(mean, 1.0) * (100.0 - Math.Max(mean, 1.0)) / currentDen);

        var ucl = Math.Min(100.0, mean + (ControlLimitSigma * effectiveSigma));

        if (currentPct > ucl)
        {
            var zScore = effectiveSigma > 0
                ? (currentPct - mean) / effectiveSigma
                : 0;

            target.Add(new ResistanceSpikeAlert
            {
                MetricType = metricType,
                MetricCode = metricCode,
                MetricName = metricName,
                OrganismLabel = organismLabel,
                CurrentWindowStart = currentStart,
                CurrentWindowEnd = today,
                CurrentNumerator = currentNum,
                CurrentDenominator = currentDen,
                BaselinePeriodsCount = validBaselineWindows.Count,
                HistoricalMean = Math.Round(mean, 2),
                HistoricalStdDev = Math.Round(stdDev, 2),
                UpperControlLimit = Math.Round(ucl, 2),
                ZScore = Math.Round(zScore, 2)
            });
        }
    }

    // ------------------------------------------------------------
    // ANTIBIOTIC RESISTANCE TREND OBSERVATIONS (MONTHLY BUCKETS)
    // ------------------------------------------------------------

    private static List<AntibioticTrendObservation> BuildAntibioticTrendObservations(
        List<WorksheetProjection> organismWorksheets,
        Dictionary<string, Dictionary<string, string>> organismASTResults,
        Dictionary<string, DateTime?> patientDatesById,
        List<Antibiotic> validAntibiotics,
        bool includeRecruitmentSplit = false)
    {
        if (organismWorksheets.Count == 0 || organismASTResults.Count == 0)
        {
            return [];
        }

        var antibioticLookup = validAntibiotics
            .Where(a => !string.IsNullOrWhiteSpace(a.AntibioticCode))
            .GroupBy(a => a.AntibioticCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => string.IsNullOrWhiteSpace(g.First().AntibioticName)
                    ? g.Key
                    : g.First().AntibioticName,
                StringComparer.OrdinalIgnoreCase);

        var buckets = new Dictionary<(string Code, string? Recruitment, int Year, int Month), (int Tested, int Resistant, int Susceptible)>();

        foreach (var worksheet in organismWorksheets)
        {
            if (string.IsNullOrWhiteSpace(worksheet.IsolateID) ||
                string.IsNullOrWhiteSpace(worksheet.PatientID))
            {
                continue;
            }

            if (!patientDatesById.TryGetValue(worksheet.PatientID, out var enrollmentDate) ||
                !enrollmentDate.HasValue)
            {
                continue;
            }

            if (!organismASTResults.TryGetValue(worksheet.IsolateID, out var astResults))
            {
                continue;
            }

            var year = enrollmentDate.Value.Year;
            var month = enrollmentDate.Value.Month;
            var recruitmentGroup = includeRecruitmentSplit
                ? ClassifyRecruitmentType(worksheet.PatientID)
                : null;

            foreach (var (code, result) in astResults)
            {
                var normalizedCode = code.ToUpperInvariant();
                var key = (normalizedCode, recruitmentGroup, year, month);
                buckets.TryGetValue(key, out var current);

                current = result switch
                {
                    "R" => (current.Tested + 1, current.Resistant + 1, current.Susceptible),
                    "S" => (current.Tested + 1, current.Resistant, current.Susceptible + 1),
                    "I" => (current.Tested + 1, current.Resistant, current.Susceptible),
                    _ => current
                };

                buckets[key] = current;
            }
        }

        return
        [
            .. buckets
                .OrderBy(kvp => kvp.Key.Year)
                .ThenBy(kvp => kvp.Key.Month)
                .ThenBy(kvp => kvp.Key.Code)
                .ThenBy(kvp => kvp.Key.Recruitment)
                .Select(kvp => new AntibioticTrendObservation
                {
                    AntibioticCode = kvp.Key.Code,
                    AntibioticName = antibioticLookup.GetValueOrDefault(kvp.Key.Code, kvp.Key.Code),
                    RecruitmentType = kvp.Key.Recruitment,
                    Year = kvp.Key.Year,
                    Month = kvp.Key.Month,
                    Tested = kvp.Value.Tested,
                    Resistant = kvp.Value.Resistant,
                    Susceptible = kvp.Value.Susceptible
                })
        ];
    }

    // ------------------------------------------------------------
    // RECRUITMENT STRATIFIED KPI SUMMARY (ACTIVE VS PASSIVE)
    // ------------------------------------------------------------

    private static List<RecruitmentStratifiedKpi> BuildRecruitmentStratifiedKpis(
        List<PatientProjection> acceptedPatients,
        List<string> rejectedPatientIds,
        List<WorksheetProjection> worksheets,
        List<Genomics> genomics,
        Dictionary<string, string> isolateToPatient,
        bool includeMrsa)
    {
        List<RecruitmentStratifiedKpi> summaries = [];

        foreach (var group in RecruitmentOrder)
        {
            var groupPatients = acceptedPatients
                .Where(p => string.Equals(ClassifyRecruitmentType(p.PatientID), group, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var groupPatientIds = groupPatients
                .Select(p => p.PatientID)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var groupRejectedCount = rejectedPatientIds
                .Count(id => string.Equals(ClassifyRecruitmentType(id), group, StringComparison.OrdinalIgnoreCase));

            var groupWorksheets = worksheets
                .Where(w => !string.IsNullOrWhiteSpace(w.PatientID) && groupPatientIds.Contains(w.PatientID))
                .ToList();

            var groupIsolateIds = groupWorksheets
                .Select(w => w.IsolateID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var groupSequencedCount = genomics
                .Count(g => !string.IsNullOrWhiteSpace(g.IsolateID) && groupIsolateIds.Contains(g.IsolateID));

            var staphWorksheets = includeMrsa
                ? groupWorksheets.Where(w => IsStaphylococcusAureus(w.WHONETID)).ToList()
                : [];

            summaries.Add(new RecruitmentStratifiedKpi
            {
                RecruitmentType = group,
                TotalPatients = groupPatients.Count,
                RejectedSamples = groupRejectedCount,
                TotalIsolates = groupWorksheets.Count,
                PositiveCultures = groupWorksheets.Count(w => IsPositiveCulture(w.CultureResult)),
                NegativeCultures = groupWorksheets.Count(w => IsNegativeCulture(w.CultureResult)),
                MDRIsolates = groupWorksheets.Count(w => w.MDR == true),
                SequencedIsolates = groupSequencedCount,
                MRSAIsolates = staphWorksheets.Count(w => w.MRSA == true),
                MRSADenominator = staphWorksheets.Count
            });
        }

        return summaries;
    }

    // ------------------------------------------------------------
    // EXPORT PACKAGE (AGGREGATED SUMMARIES ONLY - ACCEPTED SAMPLES)
    // ------------------------------------------------------------

    public async Task<DashboardExportViewModel> GetExportDataAsync(
        DashboardViewModel filters,
        CancellationToken cancellationToken = default)
    {
        var summary = await GetDashboardAsync(filters, cancellationToken);

        var export = new DashboardExportViewModel
        {
            Summary = summary
        };

        // Recruitment stratified summary if opted in
        if (summary.IsGroupedByRecruitmentType && summary.RecruitmentStratifiedKpis.Count > 0)
        {
            foreach (var r in summary.RecruitmentStratifiedKpis)
            {
                export.PatientSummaries.Add(new ExportSummaryRow
                {
                    Category = "Recruitment Strategy",
                    Group = r.RecruitmentType,
                    Count = r.TotalPatients,
                    Denominator = summary.TotalPatients,
                    Percentage = summary.TotalPatients > 0
                        ? Math.Round((double)r.TotalPatients / summary.TotalPatients * 100.0, 2)
                        : null,
                    Notes = $"Rejected: {r.RejectedSamples}; Isolates: {r.TotalIsolates}; MDR: {r.MDRIsolates}; Sequenced: {r.SequencedIsolates}"
                });
            }
        }

        // Patient summaries (accepted cohort)
        AddExportCategory(export.PatientSummaries, "Sex", summary.PatientsBySex, summary.TotalPatients);
        AddExportCategory(export.PatientSummaries, "Age Group", summary.PatientsByAgeGroup, summary.TotalPatients);
        AddExportCategory(export.PatientSummaries, "Military / Civilian", summary.PatientsByMilitaryCivilianStatus, summary.TotalPatients);
        AddExportCategory(export.PatientSummaries, "Patient Type", summary.PatientsByPatientType, summary.TotalPatients);
        AddExportCategory(export.PatientSummaries, "HAI / CAI", summary.PatientsByHAICAI, summary.TotalPatients);
        AddExportCategory(export.PatientSummaries, "Clinical Presentation", summary.PatientsByClinicalPresentation, summary.TotalPatients);

        // Isolate summaries (accepted cohort)
        AddExportCategory(export.IsolateSummaries, "Organism Distribution", summary.IsolatesByOrganism, summary.TotalIsolates);
        AddExportCategory(export.IsolateSummaries, "Culture Results", summary.CultureResults, summary.TotalIsolates);

        if (summary.ShowMRSAKpi)
        {
            export.IsolateSummaries.Add(new ExportSummaryRow
            {
                Category = "Staphylococcus aureus Resistance",
                Group = "MRSA",
                Count = summary.MRSAIsolates,
                Denominator = summary.MRSADenominator,
                Percentage = summary.MRSAPercentage
            });
        }

        if (summary.ASTOrganismIDs.Count > 0)
        {
            var organismLabel = summary.ASTOrganismName ?? string.Join(", ", summary.ASTOrganismIDs);
            var astCategory = summary.ASTOrganismIsolateCount >= 30
                ? $"Antibiogram: {organismLabel}"
                : $"AST small sample: {organismLabel}";

            foreach (var item in summary.Antibiogram)
            {
                export.IsolateSummaries.Add(
                    new ExportSummaryRow
                    {
                        Category = astCategory,
                        Group = item.AntibioticName,
                        Count = item.Tested,
                        Denominator = summary.ASTOrganismIsolateCount,
                        Percentage = item.SusceptiblePercentage,
                        Notes =
                            $"Tested: {item.Tested}; " +
                            $"S: {item.Susceptible} ({item.SusceptiblePercentage:F1}%); " +
                            $"I: {item.Intermediate} ({item.IntermediatePercentage:F1}%); " +
                            $"R: {item.Resistant} ({item.ResistantPercentage:F1}%)"
                    });
            }
        }

        // Genomics summaries (accepted cohort)
        AddExportCategory(export.GenomicsSummaries, "Sequencing Over Time", summary.SequencingOverTime, summary.SequencedIsolates);
        AddExportCategory(export.GenomicsSummaries, "Genome Completeness", summary.GenomeCompleteness, summary.SequencedIsolates);
        AddExportCategory(export.GenomicsSummaries, "Strain Types", summary.StrainTypesData, summary.SequencedIsolates);
        AddExportCategory(export.GenomicsSummaries, "Gene Prevalence", summary.GenePrevalence, summary.SequencedIsolates);

        foreach (var score in summary.GenomicScores)
        {
            export.GenomicsSummaries.Add(
                new ExportSummaryRow
                {
                    Category = "Genomic Scores",
                    Group = score.Label,
                    Count = (int)Math.Round(score.Value),
                    Denominator = score.Denominator,
                    Notes = score.Category
                });
        }

        return export;
    }

    private static void AddExportCategory(
        List<ExportSummaryRow> target,
        string category,
        IEnumerable<DashboardDataPoint> points,
        int? defaultDenominator)
    {
        foreach (var point in points)
        {
            var denominator = point.Denominator ?? defaultDenominator;
            var count = (int)Math.Round(point.Value);

            double? percentage = denominator is > 0
                ? Math.Round((double)count / denominator.Value * 100.0, 2)
                : null;

            target.Add(
                new ExportSummaryRow
                {
                    Category = category,
                    Group = point.Label,
                    Count = count,
                    Denominator = denominator,
                    Percentage = percentage,
                    Notes = point.Category
                });
        }
    }

    // ------------------------------------------------------------
    // FILTER OPTIONS & FACILITY RESOLUTION
    // ------------------------------------------------------------

    private async Task PopulateFilterOptionsAsync(
        DashboardViewModel model,
        CancellationToken cancellationToken)
    {
        model.RecruitmentTypeOptions =
        [
            new SelectListItem
            {
                Value = "Active",
                Text = "Active",
                Selected = string.Equals(model.RecruitmentType, "Active", StringComparison.OrdinalIgnoreCase)
            },
            new SelectListItem
            {
                Value = "Passive",
                Text = "Passive (ISO)",
                Selected = string.Equals(model.RecruitmentType, "Passive", StringComparison.OrdinalIgnoreCase)
            }
        ];

        var facilities = await _context.Facilities
            .AsNoTracking()
            .OrderBy(f => f.FacilityName)
            .ToListAsync(cancellationToken);

        var ownerships = facilities
            .Select(f => f.Ownership?.Trim())
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o)
            .ToList();

        model.FacilityOwnershipOptions =
        [
            .. ownerships.Select(o => new SelectListItem
            {
                Value = o!,
                Text = o!,
                Selected = string.Equals(
                    o,
                    model.FacilityOwnership,
                    StringComparison.OrdinalIgnoreCase)
            })
        ];

        var filteredFacilities = facilities.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(model.FacilityOwnership))
        {
            filteredFacilities = filteredFacilities.Where(f =>
                string.Equals(
                    f.Ownership?.Trim(),
                    model.FacilityOwnership.Trim(),
                    StringComparison.OrdinalIgnoreCase));
        }

        model.Facilities =
        [
            .. filteredFacilities.Select(f => new SelectListItem
            {
                Value = f.FacilityID,
                Text = string.IsNullOrWhiteSpace(f.FacilityName)
                    ? f.FacilityID
                    : f.FacilityName,
                Selected = model.FacilityIDs.Contains(
                    f.FacilityID,
                    StringComparer.OrdinalIgnoreCase)
            })
        ];

        var organisms = await _context.Organisms
            .AsNoTracking()
            .OrderBy(o => o.WHONETID)
            .ToListAsync(cancellationToken);

        model.Organisms =
        [
            .. organisms.Select(o => new SelectListItem
            {
                Value = o.WHONETID,
                Text = o.WHONETID,
                Selected = model.OrganismIDs.Contains(
                    o.WHONETID,
                    StringComparer.OrdinalIgnoreCase)
            })
        ];

        model.GenomicCharacteristicOptions =
        [
            new SelectListItem("All characteristics", ""),
            new SelectListItem("Strain Type", "StrainType"),
            new SelectListItem("Phylogroup", "Phylogroup"),
            new SelectListItem("Serotype", "Serotype"),
            new SelectListItem("SCCmec Type", "SCCmecType"),
            new SelectListItem("Spa Type", "SpaType"),
            new SelectListItem("O Locus", "OLocus"),
            new SelectListItem("K Locus", "KLocus")
        ];

        await LoadGenomicOptionsAsync(model, cancellationToken);
    }

    private async Task LoadGenomicOptionsAsync(
        DashboardViewModel model,
        CancellationToken cancellationToken)
    {
        var values = await _context.Genomics
            .AsNoTracking()
            .Select(g => new
            {
                g.StrainType,
                g.Phylogroup,
                g.Serotype,
                g.SCCmecType,
                g.SpaType,
                g.OLocus,
                g.KLocus
            })
            .ToListAsync(cancellationToken);

        model.StrainTypes = ToSelectList(values.Select(v => v.StrainType), model.StrainType);
        model.Phylogroups = ToSelectList(values.Select(v => v.Phylogroup), model.Phylogroup);
        model.Serotypes = ToSelectList(values.Select(v => v.Serotype), model.Serotype);
        model.SCCmecTypes = ToSelectList(values.Select(v => v.SCCmecType), model.SCCmecType);
        model.SpaTypes = ToSelectList(values.Select(v => v.SpaType), model.SpaType);
        model.OLoci = ToSelectList(values.Select(v => v.OLocus), model.OLocus);
        model.KLoci = ToSelectList(values.Select(v => v.KLocus), model.KLocus);
    }

    private static List<SelectListItem> ToSelectList(
        IEnumerable<string?> rawValues,
        string? selectedValue)
    {
        return
        [
            .. rawValues
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v)
                .Select(v => new SelectListItem
                {
                    Value = v,
                    Text = v,
                    Selected = string.Equals(
                        v,
                        selectedValue,
                        StringComparison.OrdinalIgnoreCase)
                })
        ];
    }

    private async Task<List<string>?> ResolveEffectiveFacilityIdsAsync(
        DashboardViewModel model,
        CancellationToken cancellationToken)
    {
        var hasOwnership = !string.IsNullOrWhiteSpace(model.FacilityOwnership);
        var hasExplicitFacilities = model.FacilityIDs.Count > 0;

        if (!hasOwnership && !hasExplicitFacilities)
        {
            return null;
        }

        List<string>? ownershipFacilityIds = null;

        if (hasOwnership)
        {
            var ownershipTrimmed = model.FacilityOwnership!.Trim();

            ownershipFacilityIds = await _context.Facilities
                .AsNoTracking()
                .Where(f =>
                    f.Ownership != null &&
                    EF.Functions.Collate(f.Ownership.Trim(), "SQL_Latin1_General_CP1_CI_AS") == ownershipTrimmed)
                .Select(f => f.FacilityID)
                .ToListAsync(cancellationToken);
        }

        if (hasOwnership && hasExplicitFacilities)
        {
            return
            [
                .. ownershipFacilityIds!
                    .Intersect(
                        model.FacilityIDs,
                        StringComparer.OrdinalIgnoreCase)
            ];
        }

        if (hasOwnership)
        {
            return ownershipFacilityIds;
        }

        return
        [
            .. model.FacilityIDs
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static IQueryable<Patient> ApplyRecruitmentFilter(
        IQueryable<Patient> query,
        string? recruitmentType)
    {
        if (string.Equals(recruitmentType, "Passive", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(p => p.PatientID.StartsWith("ISO"));
        }

        if (string.Equals(recruitmentType, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(p => !p.PatientID.StartsWith("ISO"));
        }

        return query;
    }

    private static string? NormalizeRecruitmentFilter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (string.Equals(trimmed, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return "Active";
        }

        if (string.Equals(trimmed, "Passive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "ISO", StringComparison.OrdinalIgnoreCase))
        {
            return "Passive";
        }

        return null;
    }

    private static string ClassifyRecruitmentType(string? patientId)
    {
        if (!string.IsNullOrWhiteSpace(patientId) &&
            patientId.Trim().StartsWith("ISO", StringComparison.OrdinalIgnoreCase))
        {
            return "Passive";
        }

        return "Active";
    }

    // ------------------------------------------------------------
    // DYNAMIC AST EXTRACTION FROM [dbo].[Worksheet] ({CODE}_INT)
    // ------------------------------------------------------------

    private async Task<Dictionary<string, Dictionary<string, string>>> LoadDynamicASTResultsByIsolateAsync(
        List<string> isolateIds,
        List<Antibiotic> validAntibiotics,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, Dictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);

        if (isolateIds.Count == 0 || validAntibiotics.Count == 0)
        {
            return results;
        }

        var candidateCodes = validAntibiotics
            .Select(a => NormalizeAntibioticCode(a.AntibioticCode))
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateCodes.Count == 0)
        {
            return results;
        }

        var existingWorksheetColumns = await GetExistingTableColumnsAsync(
            "Worksheet",
            cancellationToken);

        var activeCodeColumns = candidateCodes
            .Select(code => new
            {
                Code = code,
                IntColumn = $"{code}_INT"
            })
            .Where(x => existingWorksheetColumns.Contains(x.IntColumn))
            .ToList();

        if (activeCodeColumns.Count == 0)
        {
            return results;
        }

        var selectColumnsSql = string.Join(
            ", ",
            IsolateIdColumn.Concat(
                activeCodeColumns.Select(x => $"[{x.IntColumn}]")));

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            const int batchSize = 500;
            for (var offset = 0; offset < isolateIds.Count; offset += batchSize)
            {
                var batch = isolateIds.Skip(offset).Take(batchSize).ToList();

                await using var command = connection.CreateCommand();
                List<string> paramNames = new(batch.Count);

                for (var i = 0; i < batch.Count; i++)
                {
                    var paramName = $"@id{i}";
                    paramNames.Add(paramName);

                    var param = command.CreateParameter();
                    param.ParameterName = paramName;
                    param.Value = batch[i];
                    command.Parameters.Add(param);
                }

                command.CommandText = $"""
                    SELECT {selectColumnsSql}
                    FROM [dbo].[Worksheet]
                    WHERE [IsolateID] IN ({string.Join(", ", paramNames)});
                    """;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                var isolateOrdinal = reader.GetOrdinal("IsolateID");
                var columnOrdinals = activeCodeColumns
                    .Select(x => new
                    {
                        x.Code,
                        Ordinal = reader.GetOrdinal(x.IntColumn)
                    })
                    .ToList();

                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.IsDBNull(isolateOrdinal))
                    {
                        continue;
                    }

                    var isolateId = reader.GetString(isolateOrdinal).Trim();
                    if (string.IsNullOrWhiteSpace(isolateId))
                    {
                        continue;
                    }

                    if (!results.TryGetValue(isolateId, out var isolateAntibiotics))
                    {
                        isolateAntibiotics = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase);
                        results[isolateId] = isolateAntibiotics;
                    }

                    foreach (var col in columnOrdinals)
                    {
                        if (reader.IsDBNull(col.Ordinal))
                        {
                            continue;
                        }

                        var rawValue = reader.GetString(col.Ordinal);
                        var normalizedResult = NormalizeASTInterpretation(rawValue);

                        if (normalizedResult is not null &&
                            !isolateAntibiotics.ContainsKey(col.Code))
                        {
                            isolateAntibiotics[col.Code] = normalizedResult;
                        }
                    }
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }

    private static List<AntibiogramRow> BuildAntibiogram(
        Dictionary<string, Dictionary<string, string>> organismASTResults,
        List<Antibiotic> validAntibiotics)
    {
        var antibioticLookup = validAntibiotics
            .Where(a => !string.IsNullOrWhiteSpace(a.AntibioticCode))
            .GroupBy(a => a.AntibioticCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().AntibioticName,
                StringComparer.OrdinalIgnoreCase);

        var countsByCode = new Dictionary<string, (int S, int I, int R)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var isolate in organismASTResults.Values)
        {
            foreach (var (code, result) in isolate)
            {
                countsByCode.TryGetValue(code, out var current);

                current = result switch
                {
                    "S" => (current.S + 1, current.I, current.R),
                    "I" => (current.S, current.I + 1, current.R),
                    "R" => (current.S, current.I, current.R + 1),
                    _ => current
                };

                countsByCode[code] = current;
            }
        }

        List<AntibiogramRow> rows = [];

        foreach (var (code, counts) in countsByCode)
        {
            var tested = counts.S + counts.I + counts.R;
            if (tested == 0)
            {
                continue;
            }

            var displayName = antibioticLookup.TryGetValue(code, out var name) &&
                              !string.IsNullOrWhiteSpace(name)
                ? name
                : code;

            rows.Add(
                new AntibiogramRow
                {
                    AntibioticCode = code,
                    AntibioticName = displayName,
                    Tested = tested,
                    Susceptible = counts.S,
                    Intermediate = counts.I,
                    Resistant = counts.R
                });
        }

        return
        [
            .. rows.OrderBy(r => r.AntibioticName)
        ];
    }

    private static string? NormalizeAntibioticCode(string? antibioticCode)
    {
        if (string.IsNullOrWhiteSpace(antibioticCode))
        {
            return null;
        }

        var code = antibioticCode.Trim().ToUpperInvariant();
        return AntibioticCodeRegex().IsMatch(code) ? code : null;
    }

    private static string? NormalizeASTInterpretation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();

        if (string.Equals(value, "S", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "SUSCEPTIBLE", StringComparison.OrdinalIgnoreCase))
        {
            return "S";
        }

        if (string.Equals(value, "I", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "INTERMEDIATE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "SDD", StringComparison.OrdinalIgnoreCase))
        {
            return "I";
        }

        if (string.Equals(value, "R", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "RESISTANT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "NS", StringComparison.OrdinalIgnoreCase))
        {
            return "R";
        }

        return null;
    }

    // ------------------------------------------------------------
    // DYNAMIC GENE EXTRACTION FROM [dbo].[Genomics] (BIT COLUMNS)
    // ------------------------------------------------------------

    private async Task<Dictionary<string, HashSet<string>>> LoadDynamicGeneResultsAsync(
        List<string> sequencedIsolateIds,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);

        if (sequencedIsolateIds.Count == 0)
        {
            return results;
        }

        var registeredGenes = await _context.Set<GeneReference>()
            .AsNoTracking()
            .Where(g => g.IsActive)
            .Select(g => g.GeneName)
            .ToListAsync(cancellationToken);

        var candidateGenes = registeredGenes
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidateGenes.Count == 0)
        {
            return results;
        }

        var existingGenomicsColumns = await GetExistingTableColumnsAsync(
            "Genomics",
            cancellationToken);

        var activeGeneColumns = candidateGenes
            .Where(g => existingGenomicsColumns.Contains(g))
            .ToList();

        if (activeGeneColumns.Count == 0)
        {
            return results;
        }

        var selectColumnsSql = string.Join(
            ", ",
            IsolateIdColumn.Concat(
                activeGeneColumns.Select(g => "[" + g.Replace("]", "]]") + "]")));

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            const int batchSize = 500;
            for (var offset = 0; offset < sequencedIsolateIds.Count; offset += batchSize)
            {
                var batch = sequencedIsolateIds.Skip(offset).Take(batchSize).ToList();

                await using var command = connection.CreateCommand();
                List<string> paramNames = new(batch.Count);

                for (var i = 0; i < batch.Count; i++)
                {
                    var paramName = $"@gid{i}";
                    paramNames.Add(paramName);

                    var param = command.CreateParameter();
                    param.ParameterName = paramName;
                    param.Value = batch[i];
                    command.Parameters.Add(param);
                }

                command.CommandText = $"""
                    SELECT {selectColumnsSql}
                    FROM [dbo].[Genomics]
                    WHERE [IsolateID] IN ({string.Join(", ", paramNames)});
                    """;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                var isolateOrdinal = reader.GetOrdinal("IsolateID");
                var geneOrdinals = activeGeneColumns
                    .Select(g => new
                    {
                        GeneName = g,
                        Ordinal = reader.GetOrdinal(g)
                    })
                    .ToList();

                while (await reader.ReadAsync(cancellationToken))
                {
                    if (reader.IsDBNull(isolateOrdinal))
                    {
                        continue;
                    }

                    var isolateId = reader.GetString(isolateOrdinal).Trim();
                    if (string.IsNullOrWhiteSpace(isolateId))
                    {
                        continue;
                    }

                    if (!results.TryGetValue(isolateId, out var detectedGenes))
                    {
                        detectedGenes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        results[isolateId] = detectedGenes;
                    }

                    foreach (var col in geneOrdinals)
                    {
                        if (!reader.IsDBNull(col.Ordinal) &&
                            Convert.ToBoolean(reader.GetValue(col.Ordinal)))
                        {
                            detectedGenes.Add(col.GeneName);
                        }
                    }
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return results;
    }

    private async Task<HashSet<string>> GetExistingTableColumnsAsync(
        string tableName,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo'
                  AND TABLE_NAME = @TableName;
                """;

            var param = command.CreateParameter();
            param.ParameterName = "@TableName";
            param.Value = tableName;
            command.Parameters.Add(param);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    columns.Add(reader.GetString(0));
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return columns;
    }

    // ------------------------------------------------------------
    // GENOMICS FILTERING & SUMMARIES
    // ------------------------------------------------------------

    private static IQueryable<Genomics> ApplyGenomicFilters(
        IQueryable<Genomics> query,
        DashboardViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.StrainType))
        {
            var val = model.StrainType.Trim();
            query = query.Where(g =>
                g.StrainType != null &&
                EF.Functions.Collate(g.StrainType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.Phylogroup))
        {
            var val = model.Phylogroup.Trim();
            query = query.Where(g =>
                g.Phylogroup != null &&
                EF.Functions.Collate(g.Phylogroup.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.Serotype))
        {
            var val = model.Serotype.Trim();
            query = query.Where(g =>
                g.Serotype != null &&
                EF.Functions.Collate(g.Serotype.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.SCCmecType))
        {
            var val = model.SCCmecType.Trim();
            query = query.Where(g =>
                g.SCCmecType != null &&
                EF.Functions.Collate(g.SCCmecType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.SpaType))
        {
            var val = model.SpaType.Trim();
            query = query.Where(g =>
                g.SpaType != null &&
                EF.Functions.Collate(g.SpaType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.OLocus))
        {
            var val = model.OLocus.Trim();
            query = query.Where(g =>
                g.OLocus != null &&
                EF.Functions.Collate(g.OLocus.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.KLocus))
        {
            var val = model.KLocus.Trim();
            query = query.Where(g =>
                g.KLocus != null &&
                EF.Functions.Collate(g.KLocus.Trim(), "SQL_Latin1_General_CP1_CI_AS") == val);
        }

        if (!string.IsNullOrWhiteSpace(model.GenomicCharacteristic) &&
            !string.IsNullOrWhiteSpace(model.GenomicCharacteristicValue))
        {
            var characteristic = model.GenomicCharacteristic.Trim();
            var value = model.GenomicCharacteristicValue.Trim();

            if (string.Equals(characteristic, "StrainType", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.StrainType != null &&
                    EF.Functions.Collate(g.StrainType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "Phylogroup", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.Phylogroup != null &&
                    EF.Functions.Collate(g.Phylogroup.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "Serotype", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.Serotype != null &&
                    EF.Functions.Collate(g.Serotype.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "SCCmecType", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.SCCmecType != null &&
                    EF.Functions.Collate(g.SCCmecType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "SpaType", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.SpaType != null &&
                    EF.Functions.Collate(g.SpaType.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "OLocus", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.OLocus != null &&
                    EF.Functions.Collate(g.OLocus.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
            else if (string.Equals(characteristic, "KLocus", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g =>
                    g.KLocus != null &&
                    EF.Functions.Collate(g.KLocus.Trim(), "SQL_Latin1_General_CP1_CI_AS") == value);
            }
        }

        return query;
    }

    private static List<DashboardDataPoint> BuildSequencingOverTime(
        List<Genomics> genomics,
        Dictionary<string, string> isolateToPatient,
        Dictionary<string, DateTime?> patientDatesById)
    {
        List<DateTime> dates = [];

        foreach (var g in genomics)
        {
            if (g.SequencingDate.HasValue)
            {
                dates.Add(g.SequencingDate.Value);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(g.IsolateID) &&
                isolateToPatient.TryGetValue(g.IsolateID, out var patientId) &&
                !string.IsNullOrWhiteSpace(patientId) &&
                patientDatesById.TryGetValue(patientId, out var enrollmentDate) &&
                enrollmentDate.HasValue)
            {
                dates.Add(enrollmentDate.Value);
            }
        }

        return
        [
            .. dates
                .GroupBy(d => new { d.Year, d.Month })
                .OrderBy(g => g.Key.Year)
                .ThenBy(g => g.Key.Month)
                .Select(g => new DashboardDataPoint
                {
                    Label = new DateTime(g.Key.Year, g.Key.Month, 1)
                        .ToString("MMM yyyy", CultureInfo.InvariantCulture),
                    Value = g.Count()
                })
        ];
    }

    private static List<DashboardDataPoint> BuildGenomeCompleteness(
        List<Genomics> genomics)
    {
        if (genomics.Count == 0)
        {
            return [];
        }

        return BuildCategoricalCounts(
            genomics.Select(g => g.CompleteGenome switch
            {
                true => "Complete Genome",
                false => "Draft / Incomplete",
                null => "Not Recorded"
            }),
            preferredOrder: GenomeCompletenessOrder);
    }

    private static List<DashboardDataPoint> BuildGenePrevalence(
        Dictionary<string, HashSet<string>> geneResultsByIsolate,
        int totalSequencedIsolates,
        int topN)
    {
        if (geneResultsByIsolate.Count == 0 || totalSequencedIsolates == 0)
        {
            return [];
        }

        var geneCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var genes in geneResultsByIsolate.Values)
        {
            foreach (var gene in genes)
            {
                geneCounts[gene] = geneCounts.GetValueOrDefault(gene) + 1;
            }
        }

        return
        [
            .. geneCounts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Take(topN)
                .Select(kvp => new DashboardDataPoint
                {
                    Label = kvp.Key,
                    Value = kvp.Value,
                    Denominator = totalSequencedIsolates
                })
        ];
    }

    private static List<DashboardDataPoint> BuildGenomicScores(List<Genomics> genomics)
    {
        List<DashboardDataPoint> points = [];

        var virulenceScores = genomics
            .Where(g => g.VirulenceScore.HasValue)
            .Select(g => (double)g.VirulenceScore!.Value)
            .ToList();

        if (virulenceScores.Count > 0)
        {
            points.Add(new DashboardDataPoint
            {
                Label = "Mean Virulence Score",
                Value = Math.Round(virulenceScores.Average(), 2),
                Denominator = virulenceScores.Count,
                Category = $"Max: {virulenceScores.Max():0.##}"
            });
        }

        var resistanceScores = genomics
            .Where(g => g.ResistanceScore.HasValue)
            .Select(g => (double)g.ResistanceScore!.Value)
            .ToList();

        if (resistanceScores.Count > 0)
        {
            points.Add(new DashboardDataPoint
            {
                Label = "Mean Resistance Score",
                Value = Math.Round(resistanceScores.Average(), 2),
                Denominator = resistanceScores.Count,
                Category = $"Max: {resistanceScores.Max():0.##}"
            });
        }

        var plasmidCounts = genomics
            .Where(g => g.NoOfPlasmids.HasValue)
            .Select(g => (double)g.NoOfPlasmids!.Value)
            .ToList();

        if (plasmidCounts.Count > 0)
        {
            points.Add(new DashboardDataPoint
            {
                Label = "Mean Plasmid Count",
                Value = Math.Round(plasmidCounts.Average(), 2),
                Denominator = plasmidCounts.Count,
                Category = $"Max: {plasmidCounts.Max():0.##}"
            });
        }

        return points;
    }

    // ------------------------------------------------------------
    // NORMALIZATION & CHART AGGREGATION HELPERS
    // ------------------------------------------------------------

    private static bool IsStaphylococcusAureus(string? organism)
    {
        if (string.IsNullOrWhiteSpace(organism))
        {
            return false;
        }

        var val = organism.Trim();
        return string.Equals(val, "Staphylococcus aureus", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "S. aureus", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "SAU", StringComparison.OrdinalIgnoreCase);
    }

    private static List<DashboardDataPoint> BuildCategoricalCounts(
        IEnumerable<string> labels,
        string[]? preferredOrder = null)
    {
        var counts = labels
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.First(),
                g => g.Count(),
                StringComparer.OrdinalIgnoreCase);

        if (preferredOrder is null || preferredOrder.Length == 0)
        {
            return
            [
                .. counts
                    .OrderByDescending(kvp => kvp.Value)
                    .ThenBy(kvp => kvp.Key)
                    .Select(kvp => new DashboardDataPoint
                    {
                        Label = kvp.Key,
                        Value = kvp.Value
                    })
            ];
        }

        List<DashboardDataPoint> ordered = [];

        foreach (var item in preferredOrder)
        {
            if (counts.TryGetValue(item, out var count) && count > 0)
            {
                ordered.Add(new DashboardDataPoint
                {
                    Label = item,
                    Value = count
                });
                counts.Remove(item);
            }
        }

        foreach (var remaining in counts.OrderByDescending(kvp => kvp.Value))
        {
            ordered.Add(new DashboardDataPoint
            {
                Label = remaining.Key,
                Value = remaining.Value
            });
        }

        return ordered;
    }

    private static List<DashboardDataPoint> BuildTopCategoricalCounts(
        IEnumerable<string> labels,
        int topN)
    {
        var grouped = labels
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Label = g.First(), Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Label)
            .ToList();

        if (grouped.Count <= topN)
        {
            return
            [
                .. grouped.Select(x => new DashboardDataPoint
                {
                    Label = x.Label,
                    Value = x.Count
                })
            ];
        }

        var top = grouped.Take(topN).ToList();
        var otherCount = grouped.Skip(topN).Sum(x => x.Count);

        List<DashboardDataPoint> result =
        [
            .. top.Select(x => new DashboardDataPoint
            {
                Label = x.Label,
                Value = x.Count
            })
        ];

        if (otherCount > 0)
        {
            result.Add(new DashboardDataPoint
            {
                Label = "Other",
                Value = otherCount
            });
        }

        return result;
    }

    private static string NormalizeSex(string? sex)
    {
        if (string.IsNullOrWhiteSpace(sex))
        {
            return "Unknown";
        }

        var val = sex.Trim();

        if (string.Equals(val, "M", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(val, "MALE", StringComparison.OrdinalIgnoreCase))
        {
            return "Male";
        }

        if (string.Equals(val, "F", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(val, "FEMALE", StringComparison.OrdinalIgnoreCase))
        {
            return "Female";
        }

        return "Unknown";
    }

    private static string ClassifyAgeGroup(int? age, string? ageUnit)
    {
        if (!age.HasValue || age.Value < 0)
        {
            return "Unknown";
        }

        var unit = ageUnit?.Trim() ?? string.Empty;
        double ageInYears;

        if (unit.StartsWith("day", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unit, "d", StringComparison.OrdinalIgnoreCase))
        {
            ageInYears = age.Value / 365.25;
        }
        else if (unit.StartsWith("week", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(unit, "w", StringComparison.OrdinalIgnoreCase))
        {
            ageInYears = age.Value / 52.14;
        }
        else if (unit.StartsWith("month", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(unit, "m", StringComparison.OrdinalIgnoreCase))
        {
            ageInYears = age.Value / 12.0;
        }
        else
        {
            ageInYears = age.Value;
        }

        return ageInYears switch
        {
            < 1 => "< 1 year",
            < 5 => "1–4 years",
            < 15 => "5–14 years",
            < 25 => "15–24 years",
            < 45 => "25–44 years",
            < 65 => "45–64 years",
            _ => "65+ years"
        };
    }

    private static string NormalizeStandardLabel(
        string? raw,
        string firstOption,
        string secondOption)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        var val = raw.Trim();

        if (string.Equals(val, firstOption, StringComparison.OrdinalIgnoreCase))
        {
            return firstOption;
        }

        if (string.Equals(val, secondOption, StringComparison.OrdinalIgnoreCase))
        {
            return secondOption;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(val.ToLowerInvariant());
    }

    private static string NormalizeHAICAI(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        var val = raw.Trim();

        if (string.Equals(val, "HAI", StringComparison.OrdinalIgnoreCase) ||
            val.Contains("hospital", StringComparison.OrdinalIgnoreCase) ||
            val.Contains("healthcare", StringComparison.OrdinalIgnoreCase))
        {
            return "HAI";
        }

        if (string.Equals(val, "CAI", StringComparison.OrdinalIgnoreCase) ||
            val.Contains("community", StringComparison.OrdinalIgnoreCase))
        {
            return "CAI";
        }

        return val.ToUpperInvariant();
    }

    private static bool IsPositiveCulture(string? cultureResult)
    {
        if (string.IsNullOrWhiteSpace(cultureResult))
        {
            return false;
        }

        var val = cultureResult.Trim();

        return string.Equals(val, "positive", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "pos", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "growth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "+", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNegativeCulture(string? cultureResult)
    {
        if (string.IsNullOrWhiteSpace(cultureResult))
        {
            return false;
        }

        var val = cultureResult.Trim();

        return string.Equals(val, "negative", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "neg", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "no growth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "-", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeCultureResult(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        if (IsPositiveCulture(raw))
        {
            return "Positive";
        }

        if (IsNegativeCulture(raw))
        {
            return "Negative";
        }

        if (raw.Contains("contamin", StringComparison.OrdinalIgnoreCase))
        {
            return "Contaminated";
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
            raw.Trim().ToLowerInvariant());
    }

    private static string CleanLabel(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? "Unspecified"
            : raw.Trim();
    }

    // ------------------------------------------------------------
    // INTERNAL PROJECTION CLASSES
    // ------------------------------------------------------------

    private sealed class PatientProjection
    {
        public string PatientID { get; set; } = string.Empty;
        public string? Sex { get; set; }
        public int? Age { get; set; }
        public string? AgeUnit { get; set; }
        public string? MILCIV { get; set; }
        public string? PatientType { get; set; }
        public string? HAICAI { get; set; }
        public string? ClinicalPresentation { get; set; }
        public DateTime? EnrollmentDate { get; set; }
    }

    private sealed class WorksheetProjection
    {
        public string IsolateID { get; set; } = string.Empty;
        public string PatientID { get; set; } = string.Empty;
        public string? WHONETID { get; set; }
        public string? CultureResult { get; set; }
        public string? ESBL { get; set; }
        public bool? Carbapenemase { get; set; }
        public bool? MRSA { get; set; }
        public bool? MDR { get; set; }
    }

    private sealed class TimestampedIsolate
    {
        public string IsolateID { get; set; } = string.Empty;
        public DateTime EnrollmentDate { get; set; }
        public string WHONETID { get; set; } = string.Empty;
        public bool? MDR { get; set; }
        public bool? MRSA { get; set; }
    }
}