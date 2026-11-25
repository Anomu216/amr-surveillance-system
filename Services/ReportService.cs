using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public partial class ReportService(ApplicationDbContext context) : IReportService
{
    private readonly ApplicationDbContext _context = context;

    [GeneratedRegex(@"^[A-Z0-9_]+$")]
    private static partial Regex ValidCodeRegex();

    private static readonly HashSet<string> ESKAPEEOrganisms = new(StringComparer.OrdinalIgnoreCase)
    {
        "Enterococcus faecium",
        "Enterococcus faecalis",
        "Staphylococcus aureus",
        "Klebsiella pneumoniae",
        "Acinetobacter baumannii",
        "Pseudomonas aeruginosa",
        "Enterobacter cloacae",
        "Escherichia coli"
    };

    private static readonly HashSet<string> GramNegativeESKAPEE = new(StringComparer.OrdinalIgnoreCase)
    {
        "Klebsiella pneumoniae",
        "Acinetobacter baumannii",
        "Pseudomonas aeruginosa",
        "Enterobacter cloacae",
        "Escherichia coli"
    };

    // ============================================================
    // 21st-TO-20th PERIOD CALCULATION (ANCHORED ON ReceivingDate)
    // ============================================================

    public static (DateTime Start, DateTime End) GetSurveillanceCycleForDate(DateTime anchorDate)
    {
        var d = anchorDate.Date;
        if (d.Day >= 21)
        {
            var start = new DateTime(d.Year, d.Month, 21);
            var end = start.AddMonths(1).AddDays(-1);
            return (start, end);
        }
        else
        {
            var end = new DateTime(d.Year, d.Month, 20);
            var start = end.AddMonths(-1).AddDays(1);
            return (start, end);
        }
    }

    public static string FormatPeriodKey(DateTime start, DateTime end) =>
        $"{start:yyyy-MM-dd}_{end:yyyy-MM-dd}";

    public static string FormatPeriodLabel(DateTime start, DateTime end) =>
        $"{start:dd MMM yyyy} – {end:dd MMM yyyy}";

    // ============================================================
    // MAIN REPORT GENERATOR
    // ============================================================

    public async Task<ReportViewModel> GenerateMonthlyReportAsync(
        string? periodKey,
        DateTime? customStart,
        DateTime? customEnd,
        List<string>? facilityIds,
        List<string>? organismIds,
        string? haiCai,
        CancellationToken cancellationToken = default)
    {
        ReportViewModel model = new()
        {
            SelectedFacilityIDs = facilityIds?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],
            SelectedOrganismIDs = organismIds?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],
            SelectedHAICAI = NormalizeHAICAIFilter(haiCai)
        };

        // 1. Anchor on the latest recorded Patient.ReceivingDate (Accepted records)
        var latestReceivingDate = await _context.Patients
            .AsNoTracking()
            .Where(p => p.AcceptReject == "Accepted" && p.ReceivingDate.HasValue)
            .MaxAsync(p => (DateTime?)p.ReceivingDate, cancellationToken);

        model.LatestReceivingDate = latestReceivingDate;
        var anchorDate = (latestReceivingDate ?? DateTime.Today).Date;
        var (latestCycleStart, latestCycleEnd) = GetSurveillanceCycleForDate(anchorDate);

        // Build 12 historical 21st-to-20th period presets
        for (int i = 0; i < 12; i++)
        {
            var s = latestCycleStart.AddMonths(-i);
            var e = s.AddMonths(1).AddDays(-1);
            var key = FormatPeriodKey(s, e);
            model.AvailablePeriods.Add(new()
            {
                Value = key,
                Text = i == 0
                    ? $"{FormatPeriodLabel(s, e)} (Latest Cycle)"
                    : FormatPeriodLabel(s, e)
            });
        }

        // Resolve active reporting window
        DateTime periodStart;
        DateTime periodEnd;

        var isCustomSelected = string.Equals(periodKey, "CUSTOM", StringComparison.OrdinalIgnoreCase);

        if (isCustomSelected &&
            customStart.HasValue &&
            customEnd.HasValue &&
            customEnd.Value.Date >= customStart.Value.Date)
        {
            periodStart = customStart.Value.Date;
            periodEnd = customEnd.Value.Date;
            model.SelectedPeriodKey = "CUSTOM";
        }
        else if (!string.IsNullOrWhiteSpace(periodKey) &&
                 !isCustomSelected &&
                 TryParsePeriodKey(periodKey, out var parsedStart, out var parsedEnd))
        {
            periodStart = parsedStart;
            periodEnd = parsedEnd;
            model.SelectedPeriodKey = FormatPeriodKey(periodStart, periodEnd);
        }
        else
        {
            periodStart = latestCycleStart;
            periodEnd = latestCycleEnd;
            model.SelectedPeriodKey = FormatPeriodKey(periodStart, periodEnd);
        }

        model.PeriodStart = periodStart;
        model.PeriodEnd = periodEnd;

        // Prior period (for period-over-period comparisons)
        if (periodStart.Day == 21 && periodEnd.Day == 20)
        {
            model.PriorPeriodStart = periodStart.AddMonths(-1);
            model.PriorPeriodEnd = periodStart.AddDays(-1);
        }
        else
        {
            var spanDays = (periodEnd - periodStart).Days + 1;
            model.PriorPeriodEnd = periodStart.AddDays(-1);
            model.PriorPeriodStart = model.PriorPeriodEnd.AddDays(-(spanDays - 1));
        }

        // Load lookup lists
        var facilities = await _context.Facilities
            .AsNoTracking()
            .OrderBy(f => f.FacilityName)
            .ToListAsync(cancellationToken);

        var facilityNameMap = facilities.ToDictionary(
            f => f.FacilityID,
            f => f.FacilityName,
            StringComparer.OrdinalIgnoreCase);

        model.AvailableFacilities =
        [
            .. facilities.Select(f => new SelectListItem
            {
                Value = f.FacilityID,
                Text = $"{f.FacilityName} ({f.FacilityID})",
                Selected = model.SelectedFacilityIDs.Contains(f.FacilityID, StringComparer.OrdinalIgnoreCase)
            })
        ];

        var distinctOrganisms = await _context.Worksheets
            .AsNoTracking()
            .Where(w =>
                (w.CultureResult == "G" || w.CultureResult == "Growth") &&
                !string.IsNullOrWhiteSpace(w.WHONETID))
            .Select(w => w.WHONETID!.Trim())
            .Distinct()
            .OrderBy(o => o)
            .ToListAsync(cancellationToken);

        model.AvailableOrganisms =
        [
            .. distinctOrganisms.Select(o => new SelectListItem
            {
                Value = o,
                Text = o,
                Selected = model.SelectedOrganismIDs.Contains(o, StringComparer.OrdinalIgnoreCase)
            })
        ];

        // Load dynamic high-consequence antibiotics flagged for monthly reporting
        var reportableAntibiotics = await _context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active && a.IncludeInMonthlyReport)
            .OrderBy(a => a.AntibioticClassName)
            .ThenBy(a => a.AntibioticName)
            .ToListAsync(cancellationToken);

        // Determine full 6-period date span so we query once cleanly
        var sixPeriodWindows = BuildSixPeriodWindows(periodStart, periodEnd);
        var earliestNeededDate = new[] { sixPeriodWindows.Min(w => w.Start), model.PriorPeriodStart }.Min();
        var latestNeededDate = new[] { sixPeriodWindows.Max(w => w.End), periodEnd }.Max().AddDays(1);

        var basePatientQuery = _context.Patients
            .AsNoTracking()
            .Include(p => p.Facility)
            .Include(p => p.Worksheets)
            .Where(p =>
                p.AcceptReject == "Accepted" &&
                p.ReceivingDate.HasValue &&
                p.ReceivingDate.Value >= earliestNeededDate &&
                p.ReceivingDate.Value < latestNeededDate);

        if (model.SelectedFacilityIDs.Count > 0)
        {
            basePatientQuery = basePatientQuery.Where(p => model.SelectedFacilityIDs.Contains(p.FacilityID));
        }

        if (model.SelectedHAICAI != "All")
        {
            basePatientQuery = basePatientQuery.Where(p => p.HAICAI == model.SelectedHAICAI);
        }

        var patients = await basePatientQuery.ToListAsync(cancellationToken);

        // Flatten recovered isolates (CultureResult == "G" or "Growth", applying organism filter if selected)
        var allIsolates = FlattenGrowthIsolates(patients, facilityNameMap, model.SelectedOrganismIDs);

        // Load AST interpretations for all matching growth isolates across the 6-period window
        var allIsolateIds = allIsolates
            .Select(r => r.IsolateID)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var astMap = await LoadASTInterpretationsAsync(allIsolateIds, reportableAntibiotics, cancellationToken);

        // Partition into Current Period vs Prior Period
        var currentPatients = patients
            .Where(p => p.ReceivingDate!.Value.Date >= periodStart && p.ReceivingDate!.Value.Date <= periodEnd)
            .ToList();

        var currentIsolates = allIsolates
            .Where(r => r.ReceivingDate >= periodStart && r.ReceivingDate <= periodEnd)
            .ToList();

        var priorIsolates = allIsolates
            .Where(r => r.ReceivingDate >= model.PriorPeriodStart && r.ReceivingDate <= model.PriorPeriodEnd)
            .ToList();

        // Populate Patients, Recovered Isolates (G / Growth), ESKAPE+, Facility & Organism tables
        PopulatePatientsIsolatesAndDistributions(model, currentPatients, currentIsolates, facilityNameMap);

        // Populate MDR, MRSA, CRE, ESBL comparisons
        PopulateResistanceIndicators(model, currentIsolates, priorIsolates, facilityNameMap);

        // Populate Dynamic High-Consequence Antibiotics
        PopulateReportableAntibiotics(model, currentIsolates, priorIsolates, reportableAntibiotics, astMap, facilityNameMap);

        // Populate 6-Period Rollup & Spike Alerts
        PopulateSixPeriodRollup(model, sixPeriodWindows, patients, allIsolates, reportableAntibiotics, astMap);
        model.ReportRollupSpikes = await DetectMonthlyRollupSpikesAsync(cancellationToken);

        // Flag small-sample caveats (< 15)
        model.HasSmallSampleCaveats =
            model.GramNegativeMDR.IsSmallSample ||
            model.OverallMDR.IsSmallSample ||
            model.MRSA.IsSmallSample ||
            model.CRE.IsSmallSample ||
            model.ESBL.IsSmallSample ||
            model.ReportableAntibiotics.Any(a => a.IsSmallSample);

        // Generate Auto-Narrative matching study report style
        model.GeneratedNarrative = BuildStudyReportNarrative(model);

        return model;
    }

    // ============================================================
    // MONTHLY ROLLUP SPIKE DETECTION (LINKED TO NOTIFICATIONS)
    // ============================================================

    public async Task<List<MonthlyRollupSpikeAlert>> DetectMonthlyRollupSpikesAsync(
        CancellationToken cancellationToken = default)
    {
        var latestReceivingDate = await _context.Patients
            .AsNoTracking()
            .Where(p => p.AcceptReject == "Accepted" && p.ReceivingDate.HasValue)
            .MaxAsync(p => (DateTime?)p.ReceivingDate, cancellationToken);

        if (!latestReceivingDate.HasValue)
        {
            return [];
        }

        var (latestStart, latestEnd) = GetSurveillanceCycleForDate(latestReceivingDate.Value.Date);
        var sixWindows = BuildSixPeriodWindows(latestStart, latestEnd);
        var earliest = sixWindows.Min(w => w.Start);
        var exclusiveEnd = latestEnd.AddDays(1);

        var reportableAntibiotics = await _context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active && a.IncludeInMonthlyReport)
            .ToListAsync(cancellationToken);

        var patients = await _context.Patients
            .AsNoTracking()
            .Include(p => p.Facility)
            .Include(p => p.Worksheets)
            .Where(p =>
                p.AcceptReject == "Accepted" &&
                p.ReceivingDate.HasValue &&
                p.ReceivingDate.Value >= earliest &&
                p.ReceivingDate.Value < exclusiveEnd)
            .ToListAsync(cancellationToken);

        var facilityMap = await _context.Facilities
            .AsNoTracking()
            .ToDictionaryAsync(f => f.FacilityID, f => f.FacilityName, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var isolates = FlattenGrowthIsolates(patients, facilityMap, []);
        var isolateIds = isolates
            .Select(r => r.IsolateID)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var astMap = await LoadASTInterpretationsAsync(isolateIds, reportableAntibiotics, cancellationToken);

        List<MonthlyRollupSpikeAlert> alerts = [];
        const int minCycleDenominator = 15;

        // 1. Cohort-wide Gram-negative ESKAPEE MDR spike
        EvaluateSixCycleControlLimit(
            alerts,
            sixWindows,
            minCycleDenominator,
            metricType: "MDR",
            metricCode: "GN_MDR",
            metricName: "Gram-Negative ESKAPEE MDR",
            organismLabel: "Gram-Negative ESKAPEE",
            statsForWindow: (s, e) =>
            {
                var gn = isolates.Where(r => r.ReceivingDate >= s && r.ReceivingDate <= e && r.IsGramNegativeESKAPEE && r.MDR.HasValue).ToList();
                return (gn.Count(r => r.MDR == true), gn.Count);
            });

        // 2. MRSA spike in Staphylococcus aureus
        EvaluateSixCycleControlLimit(
            alerts,
            sixWindows,
            minCycleDenominator,
            metricType: "MRSA",
            metricCode: "MRSA",
            metricName: "Methicillin-Resistant S. aureus (MRSA)",
            organismLabel: "Staphylococcus aureus",
            statsForWindow: (s, e) =>
            {
                var sa = isolates.Where(r => r.ReceivingDate >= s && r.ReceivingDate <= e &&
                    string.Equals(r.WHONETID, "Staphylococcus aureus", StringComparison.OrdinalIgnoreCase) &&
                    r.MRSA.HasValue).ToList();
                return (sa.Count(r => r.MRSA == true), sa.Count);
            });

        // 3. CRE spike (Gram-negative isolates)
        EvaluateSixCycleControlLimit(
            alerts,
            sixWindows,
            minCycleDenominator,
            metricType: "CRE",
            metricCode: "CRE",
            metricName: "Carbapenemase / CRE",
            organismLabel: "Gram-Negative Isolates",
            statsForWindow: (s, e) =>
            {
                var cre = isolates.Where(r => r.ReceivingDate >= s && r.ReceivingDate <= e && r.Carbapenemase.HasValue).ToList();
                return (cre.Count(r => r.Carbapenemase == true), cre.Count);
            });

        // 4. ESBL spike
        EvaluateSixCycleControlLimit(
            alerts,
            sixWindows,
            minCycleDenominator,
            metricType: "ESBL",
            metricCode: "ESBL",
            metricName: "ESBL Production",
            organismLabel: "Gram-Negative Isolates",
            statsForWindow: (s, e) =>
            {
                var esbl = isolates.Where(r => r.ReceivingDate >= s && r.ReceivingDate <= e &&
                    (string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(r.ESBL, "NEG", StringComparison.OrdinalIgnoreCase))).ToList();
                return (esbl.Count(r => string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase)), esbl.Count);
            });

        // 5. Dynamic Monthly-Report Antibiotics per Organism
        var distinctOrganisms = isolates
            .Where(r => !string.IsNullOrWhiteSpace(r.WHONETID))
            .Select(r => r.WHONETID!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var organism in distinctOrganisms)
        {
            foreach (var abx in reportableAntibiotics)
            {
                var code = NormalizeCode(abx.AntibioticCode);
                if (code == null) continue;

                EvaluateSixCycleControlLimit(
                    alerts,
                    sixWindows,
                    minCycleDenominator,
                    metricType: "ReportAntibiotic",
                    metricCode: code,
                    metricName: $"{abx.AntibioticName} Resistance",
                    organismLabel: organism,
                    statsForWindow: (s, e) =>
                    {
                        int tested = 0, resistant = 0;
                        foreach (var r in isolates.Where(x =>
                            x.ReceivingDate >= s &&
                            x.ReceivingDate <= e &&
                            string.Equals(x.WHONETID, organism, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (astMap.TryGetValue(r.IsolateID, out var res) &&
                                res.TryGetValue(code, out var interp) &&
                                !string.IsNullOrWhiteSpace(interp))
                            {
                                tested++;
                                if (string.Equals(interp, "R", StringComparison.OrdinalIgnoreCase))
                                {
                                    resistant++;
                                }
                            }
                        }
                        return (resistant, tested);
                    });
            }
        }

        return
        [
            .. alerts
                .OrderByDescending(a => a.ZScore)
                .ThenByDescending(a => a.DeltaPercentagePoints)
        ];
    }

    // ============================================================
    // INTERNAL POPULATION HELPERS
    // ============================================================

    private static void PopulatePatientsIsolatesAndDistributions(
        ReportViewModel model,
        List<Patient> currentPatients,
        List<IsolateFlatRecord> currentIsolates,
        Dictionary<string, string> facilityNameMap)
    {
        var passivePatients = currentPatients.Where(p => p.IsISOCase).ToList();
        var activePatients = currentPatients.Where(p => !p.IsISOCase).ToList();

        model.PassivePatientCount = passivePatients
            .Select(p => p.PatientID.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        model.ActivePatientCount = activePatients
            .Select(p => ExtractActiveParticipantKey(p.PatientID))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        model.TotalPatients = model.PassivePatientCount + model.ActivePatientCount;

        // Isolates = Worksheets with CultureResult == "G" or "Growth"
        model.TotalIsolatesRecovered = currentIsolates.Count;
        model.ActiveIsolatesRecovered = currentIsolates.Count(r => !r.IsPassive);
        model.PassiveIsolatesRecovered = currentIsolates.Count(r => r.IsPassive);

        // ESKAPE+ = Growth isolates whose WHONETID is in ESKAPEEOrganisms
        var eskapeeIsolates = currentIsolates.Where(r => r.IsESKAPEE).ToList();
        model.TotalESKAPEERecovered = eskapeeIsolates.Count;
        model.ActiveESKAPEERecovered = eskapeeIsolates.Count(r => !r.IsPassive);
        model.PassiveESKAPEERecovered = eskapeeIsolates.Count(r => r.IsPassive);

        model.HAICount = eskapeeIsolates.Count(r => string.Equals(r.HAICAI, "HAI", StringComparison.OrdinalIgnoreCase));
        model.CAICount = eskapeeIsolates.Count(r => string.Equals(r.HAICAI, "CAI", StringComparison.OrdinalIgnoreCase));
        model.UnknownHAICAICount = eskapeeIsolates.Count - model.HAICount - model.CAICount;

        var allFacilityIds = currentPatients
            .Select(p => p.FacilityID)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => facilityNameMap.GetValueOrDefault(id, id))
            .ToList();

        foreach (var fid in allFacilityIds)
        {
            var facPatients = currentPatients.Where(p => string.Equals(p.FacilityID, fid, StringComparison.OrdinalIgnoreCase)).ToList();
            var facIsolates = currentIsolates.Where(r => string.Equals(r.FacilityID, fid, StringComparison.OrdinalIgnoreCase)).ToList();

            var facPassiveCount = facPatients
                .Where(p => p.IsISOCase)
                .Select(p => p.PatientID.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var facActiveCount = facPatients
                .Where(p => !p.IsISOCase)
                .Select(p => ExtractActiveParticipantKey(p.PatientID))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            model.FacilityBreakdown.Add(new()
            {
                FacilityID = fid,
                FacilityName = facilityNameMap.GetValueOrDefault(fid, fid),
                PassivePatients = facPassiveCount,
                ActivePatients = facActiveCount,
                TotalPatients = facPassiveCount + facActiveCount,
                IsolatesRecovered = facIsolates.Count,
                ESKAPEERecovered = facIsolates.Count(r => r.IsESKAPEE),
                MDRCount = facIsolates.Count(r => r.MDR == true),
                MRSACount = facIsolates.Count(r => r.MRSA == true),
                CRECount = facIsolates.Count(r => r.Carbapenemase == true),
                ESBLCount = facIsolates.Count(r => string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase))
            });
        }

        var orgGroups = currentIsolates
            .Where(r => !string.IsNullOrWhiteSpace(r.WHONETID))
            .GroupBy(r => r.WHONETID!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key);

        foreach (var g in orgGroups)
        {
            var items = g.ToList();
            OrganismDistributionRow row = new()
            {
                WHONETID = g.Key,
                IsESKAPEE = ESKAPEEOrganisms.Contains(g.Key),
                IsGramNegativeESKAPEE = GramNegativeESKAPEE.Contains(g.Key),
                Count = items.Count,
                HAICount = items.Count(x => string.Equals(x.HAICAI, "HAI", StringComparison.OrdinalIgnoreCase)),
                CAICount = items.Count(x => string.Equals(x.HAICAI, "CAI", StringComparison.OrdinalIgnoreCase)),
                MDRCount = items.Count(x => x.MDR == true),
                CRECount = items.Count(x => x.Carbapenemase == true),
                ESBLCount = items.Count(x => string.Equals(x.ESBL, "POS", StringComparison.OrdinalIgnoreCase)),
                MRSACount = items.Count(x => x.MRSA == true)
            };

            if (row.IsESKAPEE)
            {
                model.OrganismBreakdown.Add(row);
            }
            else
            {
                model.NotableOtherOrganisms.Add(row);
            }
        }
    }

    private static void PopulateResistanceIndicators(
        ReportViewModel model,
        List<IsolateFlatRecord> currentIsolates,
        List<IsolateFlatRecord> priorIsolates,
        Dictionary<string, string> facilityNameMap)
    {
        model.GramNegativeMDR = BuildComparisonCard(
            title: "Gram-Negative ESKAPEE MDR",
            subtitle: "Multidrug resistance among Gram-negative ESKAPEE isolates",
            currentSubset: [.. currentIsolates.Where(r => r.IsGramNegativeESKAPEE && r.MDR.HasValue)],
            priorSubset: [.. priorIsolates.Where(r => r.IsGramNegativeESKAPEE && r.MDR.HasValue)],
            isPositive: r => r.MDR == true,
            facilityNameMap: facilityNameMap);

        model.OverallMDR = BuildComparisonCard(
            title: "Overall MDR (All Evaluated Isolates)",
            subtitle: "All recovered isolates (G) with completed MDR assessment",
            currentSubset: [.. currentIsolates.Where(r => r.MDR.HasValue)],
            priorSubset: [.. priorIsolates.Where(r => r.MDR.HasValue)],
            isPositive: r => r.MDR == true,
            facilityNameMap: facilityNameMap);

        model.MRSA = BuildComparisonCard(
            title: "MRSA (Staphylococcus aureus)",
            subtitle: "Methicillin resistance among S. aureus isolates",
            currentSubset: [.. currentIsolates.Where(r =>
                string.Equals(r.WHONETID, "Staphylococcus aureus", StringComparison.OrdinalIgnoreCase) &&
                r.MRSA.HasValue)],
            priorSubset: [.. priorIsolates.Where(r =>
                string.Equals(r.WHONETID, "Staphylococcus aureus", StringComparison.OrdinalIgnoreCase) &&
                r.MRSA.HasValue)],
            isPositive: r => r.MRSA == true,
            facilityNameMap: facilityNameMap);

        model.CRE = BuildComparisonCard(
            title: "CRE (Carbapenemase / Carbapenem-Resistant)",
            subtitle: "Isolates positive for carbapenemase / CRE phenotype",
            currentSubset: [.. currentIsolates.Where(r => r.Carbapenemase.HasValue)],
            priorSubset: [.. priorIsolates.Where(r => r.Carbapenemase.HasValue)],
            isPositive: r => r.Carbapenemase == true,
            facilityNameMap: facilityNameMap);

        model.ESBL = BuildComparisonCard(
            title: "ESBL (Extended-Spectrum β-Lactamase)",
            subtitle: "Isolates tested for ESBL production (POS vs NEG)",
            currentSubset: [.. currentIsolates.Where(r =>
                string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.ESBL, "NEG", StringComparison.OrdinalIgnoreCase))],
            priorSubset: [.. priorIsolates.Where(r =>
                string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.ESBL, "NEG", StringComparison.OrdinalIgnoreCase))],
            isPositive: r => string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase),
            facilityNameMap: facilityNameMap);
    }

    private static MetricComparisonCard BuildComparisonCard(
        string title,
        string subtitle,
        List<IsolateFlatRecord> currentSubset,
        List<IsolateFlatRecord> priorSubset,
        Func<IsolateFlatRecord, bool> isPositive,
        Dictionary<string, string> facilityNameMap)
    {
        var haiCurrent = currentSubset.Where(r => string.Equals(r.HAICAI, "HAI", StringComparison.OrdinalIgnoreCase)).ToList();
        var caiCurrent = currentSubset.Where(r => string.Equals(r.HAICAI, "CAI", StringComparison.OrdinalIgnoreCase)).ToList();

        return new()
        {
            Title = title,
            Subtitle = subtitle,
            Numerator = currentSubset.Count(isPositive),
            Denominator = currentSubset.Count,
            PriorNumerator = priorSubset.Count(isPositive),
            PriorDenominator = priorSubset.Count,
            HAINumerator = haiCurrent.Count(isPositive),
            HAIDenominator = haiCurrent.Count,
            CAINumerator = caiCurrent.Count(isPositive),
            CAIDenominator = caiCurrent.Count,
            ByOrganism =
            [
                .. currentSubset
                    .Where(r => !string.IsNullOrWhiteSpace(r.WHONETID))
                    .GroupBy(r => r.WHONETID!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new OrganismMetricBreakdown
                    {
                        Organism = g.Key,
                        Numerator = g.Count(isPositive),
                        Denominator = g.Count()
                    })
                    .Where(x => x.Numerator > 0 || x.Denominator > 0)
                    .OrderByDescending(x => x.Numerator)
                    .ThenByDescending(x => x.Denominator)
            ],
            ByFacility =
            [
                .. currentSubset
                    .GroupBy(r => r.FacilityID, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new FacilityMetricBreakdown
                    {
                        FacilityID = g.Key,
                        FacilityName = facilityNameMap.GetValueOrDefault(g.Key, g.Key),
                        Numerator = g.Count(isPositive),
                        Denominator = g.Count()
                    })
                    .OrderByDescending(x => x.Numerator)
                    .ThenBy(x => x.FacilityName)
            ]
        };
    }

    private static void PopulateReportableAntibiotics(
        ReportViewModel model,
        List<IsolateFlatRecord> currentIsolates,
        List<IsolateFlatRecord> priorIsolates,
        List<Antibiotic> reportableAntibiotics,
        Dictionary<string, Dictionary<string, string>> astMap,
        Dictionary<string, string> facilityNameMap)
    {
        HashSet<string> trackedCodes = new(StringComparer.OrdinalIgnoreCase);

        foreach (var abx in reportableAntibiotics)
        {
            var code = NormalizeCode(abx.AntibioticCode);
            if (code == null) continue;
            trackedCodes.Add(code);

            var testedCurrent = currentIsolates
                .Where(r => astMap.TryGetValue(r.IsolateID, out var res) &&
                            res.TryGetValue(code, out var interp) &&
                            !string.IsNullOrWhiteSpace(interp))
                .ToList();

            var testedPrior = priorIsolates
                .Where(r => astMap.TryGetValue(r.IsolateID, out var res) &&
                            res.TryGetValue(code, out var interp) &&
                            !string.IsNullOrWhiteSpace(interp))
                .ToList();

            bool IsResistant(IsolateFlatRecord r) =>
                astMap.TryGetValue(r.IsolateID, out var res) &&
                res.TryGetValue(code, out var interp) &&
                string.Equals(interp, "R", StringComparison.OrdinalIgnoreCase);

            var haiTested = testedCurrent.Where(r => string.Equals(r.HAICAI, "HAI", StringComparison.OrdinalIgnoreCase)).ToList();
            var caiTested = testedCurrent.Where(r => string.Equals(r.HAICAI, "CAI", StringComparison.OrdinalIgnoreCase)).ToList();

            model.ReportableAntibiotics.Add(new()
            {
                AntibioticCode = code,
                AntibioticName = abx.AntibioticName,
                AntibioticClassName = abx.AntibioticClassName,
                ResistantCount = testedCurrent.Count(IsResistant),
                TestedCount = testedCurrent.Count,
                PriorResistantCount = testedPrior.Count(IsResistant),
                PriorTestedCount = testedPrior.Count,
                HAIResistant = haiTested.Count(IsResistant),
                HAITested = haiTested.Count,
                CAIResistant = caiTested.Count(IsResistant),
                CAITested = caiTested.Count,
                ByOrganism =
                [
                    .. testedCurrent
                        .Where(r => !string.IsNullOrWhiteSpace(r.WHONETID))
                        .GroupBy(r => r.WHONETID!, StringComparer.OrdinalIgnoreCase)
                        .Select(g => new OrganismMetricBreakdown
                        {
                            Organism = g.Key,
                            Numerator = g.Count(IsResistant),
                            Denominator = g.Count()
                        })
                        .OrderByDescending(x => x.Numerator)
                        .ThenByDescending(x => x.Denominator)
                ],
                ByFacility =
                [
                    .. testedCurrent
                        .GroupBy(r => r.FacilityID, StringComparer.OrdinalIgnoreCase)
                        .Select(g => new FacilityMetricBreakdown
                        {
                            FacilityID = g.Key,
                            FacilityName = facilityNameMap.GetValueOrDefault(g.Key, g.Key),
                            Numerator = g.Count(IsResistant),
                            Denominator = g.Count()
                        })
                        .OrderByDescending(x => x.Numerator)
                        .ThenBy(x => x.FacilityName)
                ]
            });
        }

        if (trackedCodes.Count > 0)
        {
            var isolatesTestedForAnyTracked = currentIsolates
                .Where(r => astMap.TryGetValue(r.IsolateID, out var res) &&
                            trackedCodes.Any(c => res.ContainsKey(c) && !string.IsNullOrWhiteSpace(res[c])))
                .ToList();

            model.MultiReserveResistantDenominator = isolatesTestedForAnyTracked.Count;
            model.MultiReserveResistantCount = isolatesTestedForAnyTracked
                .Count(r => trackedCodes.Any(c =>
                    astMap[r.IsolateID].TryGetValue(c, out var interp) &&
                    string.Equals(interp, "R", StringComparison.OrdinalIgnoreCase)));
        }
    }

    private static void PopulateSixPeriodRollup(
        ReportViewModel model,
        List<(DateTime Start, DateTime End)> sixPeriodWindows,
        List<Patient> allPatients,
        List<IsolateFlatRecord> allIsolates,
        List<Antibiotic> reportableAntibiotics,
        Dictionary<string, Dictionary<string, string>> astMap)
    {
        foreach (var (start, end) in sixPeriodWindows)
        {
            var patientSlice = allPatients
                .Where(p => p.ReceivingDate!.Value.Date >= start && p.ReceivingDate!.Value.Date <= end)
                .ToList();

            var passivePatientsCount = patientSlice
                .Where(p => p.IsISOCase)
                .Select(p => p.PatientID.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var activePatientsCount = patientSlice
                .Where(p => !p.IsISOCase)
                .Select(p => ExtractActiveParticipantKey(p.PatientID))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var slice = allIsolates.Where(r => r.ReceivingDate >= start && r.ReceivingDate <= end).ToList();
            var gnMdr = slice.Where(r => r.IsGramNegativeESKAPEE && r.MDR.HasValue).ToList();
            var saMrsa = slice.Where(r =>
                string.Equals(r.WHONETID, "Staphylococcus aureus", StringComparison.OrdinalIgnoreCase) &&
                r.MRSA.HasValue).ToList();
            var cre = slice.Where(r => r.Carbapenemase.HasValue).ToList();
            var esbl = slice.Where(r =>
                string.Equals(r.ESBL, "POS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.ESBL, "NEG", StringComparison.OrdinalIgnoreCase)).ToList();

            MonthlyRollupPeriodPoint point = new()
            {
                PeriodKey = FormatPeriodKey(start, end),
                PeriodLabel = $"{start:dd MMM} – {end:dd MMM yy}",
                StartDate = start,
                EndDate = end,
                TotalPatients = passivePatientsCount + activePatientsCount,
                IsolatesRecovered = slice.Count,
                ESKAPEECount = slice.Count(s => s.IsESKAPEE),
                GnMDRNumerator = gnMdr.Count(s => s.MDR == true),
                GnMDRDenominator = gnMdr.Count,
                MRSANumerator = saMrsa.Count(s => s.MRSA == true),
                MRSADenominator = saMrsa.Count,
                CRENumerator = cre.Count(s => s.Carbapenemase == true),
                CREDenominator = cre.Count,
                ESBLNumerator = esbl.Count(s => string.Equals(s.ESBL, "POS", StringComparison.OrdinalIgnoreCase)),
                ESBLDenominator = esbl.Count
            };

            foreach (var abx in reportableAntibiotics)
            {
                var code = NormalizeCode(abx.AntibioticCode);
                if (code == null) continue;

                int tested = 0, resistant = 0;
                foreach (var r in slice)
                {
                    if (astMap.TryGetValue(r.IsolateID, out var res) &&
                        res.TryGetValue(code, out var interp) &&
                        !string.IsNullOrWhiteSpace(interp))
                    {
                        tested++;
                        if (string.Equals(interp, "R", StringComparison.OrdinalIgnoreCase))
                        {
                            resistant++;
                        }
                    }
                }

                point.AntibioticDenominators[code] = tested;
                point.AntibioticRates[code] = tested == 0 ? 0 : Math.Round((double)resistant / tested * 100.0, 1);
            }

            model.SixPeriodRollup.Add(point);
        }
    }

    private static string BuildStudyReportNarrative(ReportViewModel m)
    {
        StringBuilder sb = new();
        sb.AppendLine($"Multi-Pathogen Antimicrobial Resistance Surveillance Study Report ({FormatPeriodLabel(m.PeriodStart, m.PeriodEnd)})");
        sb.AppendLine();

        var passiveFacilities = m.FacilityBreakdown.Where(f => f.PassivePatients > 0)
            .Select(f => $"{f.FacilityName} ({f.FacilityID}; n = {f.PassivePatients})")
            .ToList();

        var activeFacilities = m.FacilityBreakdown.Where(f => f.ActivePatients > 0)
            .Select(f => $"{f.FacilityName} ({f.FacilityID}; n = {f.ActivePatients})")
            .ToList();

        sb.Append($"Between {m.PeriodStart:dd MMM yyyy} and {m.PeriodEnd:dd MMM yyyy} (by sample receiving date), ");
        sb.Append($"{m.PassivePatientCount} passive surveillance patient cases");
        if (passiveFacilities.Count > 0)
        {
            sb.Append($" were recorded across {passiveFacilities.Count} site(s): {string.Join(", ", passiveFacilities)}");
        }
        sb.Append($". During the same period, {m.ActivePatientCount} active surveillance patients were enrolled");
        if (activeFacilities.Count > 0)
        {
            sb.Append($" across {string.Join(", ", activeFacilities)}");
        }
        sb.AppendLine($", bringing the total surveillance cohort to {m.TotalPatients} patients.");
        sb.AppendLine();

        var orgSummary = m.OrganismBreakdown.Select(o => $"{o.Count} {o.WHONETID}").ToList();
        sb.Append($"From this cohort, {m.TotalIsolatesRecovered} culture-positive bacterial isolates (Culture Result = G) were recovered ");
        sb.Append($"({m.ActiveIsolatesRecovered} from active surveillance and {m.PassiveIsolatesRecovered} from passive surveillance). ");
        sb.Append($"Of the {m.TotalIsolatesRecovered} recovered isolates, {m.TotalESKAPEERecovered} ({m.OverallESKAPEEShareOfIsolates:F1}%) belonged to the ESKAPE+ priority group");
        if (orgSummary.Count > 0)
        {
            sb.Append($", comprising {string.Join(", ", orgSummary)}");
        }
        sb.Append($". ESKAPE+ accounted for {m.ActiveESKAPEERecovered}/{m.ActiveIsolatesRecovered} ({m.ActiveESKAPEEShareOfIsolates:F1}%) of active surveillance isolates and {m.PassiveESKAPEERecovered}/{m.PassiveIsolatesRecovered} ({m.PassiveESKAPEEShareOfIsolates:F1}%) of passive surveillance isolates. ");
        sb.AppendLine($"Among ESKAPE+ isolates, {m.HAICount} were associated with hospital-acquired infections (HAI) and {m.CAICount} with community-acquired infections (CAI).");
        sb.AppendLine();

        if (m.ReportableAntibiotics.Count > 0)
        {
            var abxPhrases = m.ReportableAntibiotics
                .Where(a => a.TestedCount > 0)
                .Select(a => $"{a.AntibioticName}: {a.ResistantCount}/{a.TestedCount} ({a.ResistanceRate:F1}%{(a.IsSmallSample ? "*" : "")})")
                .ToList();

            if (abxPhrases.Count > 0)
            {
                sb.AppendLine($"High-consequence / reserve antimicrobial resistance: {string.Join("; ", abxPhrases)}.");
                sb.AppendLine();
            }
        }

        sb.AppendLine(
            $"Among Gram-negative ESKAPEE isolates, {m.GramNegativeMDR.Numerator}/{m.GramNegativeMDR.Denominator} ({m.GramNegativeMDR.Rate:F1}%{(m.GramNegativeMDR.IsSmallSample ? "*" : "")}) were multidrug-resistant (MDR), compared with {m.GramNegativeMDR.PriorNumerator}/{m.GramNegativeMDR.PriorDenominator} ({m.GramNegativeMDR.PriorRate:F1}%) in the preceding period (HAI: {m.GramNegativeMDR.HAINumerator}/{m.GramNegativeMDR.HAIDenominator} [{m.GramNegativeMDR.HAIRate:F1}%]; CAI: {m.GramNegativeMDR.CAINumerator}/{m.GramNegativeMDR.CAIDenominator} [{m.GramNegativeMDR.CAIRate:F1}%]).");

        sb.AppendLine(
            $"Among Staphylococcus aureus isolates, {m.MRSA.Numerator}/{m.MRSA.Denominator} ({m.MRSA.Rate:F1}%{(m.MRSA.IsSmallSample ? "*" : "")}) were methicillin-resistant (MRSA), compared with {m.MRSA.PriorNumerator}/{m.MRSA.PriorDenominator} ({m.MRSA.PriorRate:F1}%) previously (HAI: {m.MRSA.HAINumerator}/{m.MRSA.HAIDenominator}; CAI: {m.MRSA.CAINumerator}/{m.MRSA.CAIDenominator}).");

        sb.AppendLine(
            $"Carbapenemase / CRE phenotype was identified in {m.CRE.Numerator}/{m.CRE.Denominator} ({m.CRE.Rate:F1}%{(m.CRE.IsSmallSample ? "*" : "")}) evaluated isolates (prior period: {m.CRE.PriorNumerator}/{m.CRE.PriorDenominator} [{m.CRE.PriorRate:F1}%]), while ESBL production was detected in {m.ESBL.Numerator}/{m.ESBL.Denominator} ({m.ESBL.Rate:F1}%{(m.ESBL.IsSmallSample ? "*" : "")}) tested isolates (prior period: {m.ESBL.PriorNumerator}/{m.ESBL.PriorDenominator} [{m.ESBL.PriorRate:F1}%]).");

        if (m.HasSmallSampleCaveats)
        {
            sb.AppendLine();
            sb.AppendLine("*Statistical Caution: Proportions marked with an asterisk (*) are based on fewer than 15 isolates (n < 15) and should be interpreted with caution.");
        }

        return sb.ToString();
    }

    // ============================================================
    // DATA & CONTROL LIMIT HELPERS
    // ============================================================

    private static List<(DateTime Start, DateTime End)> BuildSixPeriodWindows(DateTime currentStart, DateTime currentEnd)
    {
        List<(DateTime Start, DateTime End)> windows = [];
        for (int i = 5; i >= 0; i--)
        {
            if (currentStart.Day == 21 && currentEnd.Day == 20)
            {
                var s = currentStart.AddMonths(-i);
                var e = s.AddMonths(1).AddDays(-1);
                windows.Add((s, e));
            }
            else
            {
                var span = (currentEnd - currentStart).Days + 1;
                var s = currentStart.AddDays(-i * span);
                var e = s.AddDays(span - 1);
                windows.Add((s, e));
            }
        }
        return windows;
    }

    private static void EvaluateSixCycleControlLimit(
    List<MonthlyRollupSpikeAlert> alerts,
    List<(DateTime Start, DateTime End)> sixWindows,
    int minSampleSize,
    string metricType,
    string metricCode,
    string metricName,
    string organismLabel,
    Func<DateTime, DateTime, (int Resistant, int Tested)> statsForWindow)
    {
        if (sixWindows.Count < 4) return;

        var (currentStart, currentEnd) = sixWindows[^1];
        var (currRes, currTested) = statsForWindow(currentStart, currentEnd);
        if (currTested < minSampleSize) return;

        List<double> baselineRates = [];
        List<int> baselineDenominators = [];

        for (int i = 0; i < sixWindows.Count - 1; i++)
        {
            var (res, tested) = statsForWindow(sixWindows[i].Start, sixWindows[i].End);
            if (tested >= minSampleSize)
            {
                baselineRates.Add((double)res / tested * 100.0);
                baselineDenominators.Add(tested);
            }
        }

        if (baselineRates.Count < 3) return;

        var currentRate = (double)currRes / currTested * 100.0;
        var mean = baselineRates.Average();
        var variance = baselineRates.Sum(r => Math.Pow(r - mean, 2)) / (baselineRates.Count - 1);
        var stdDev = Math.Sqrt(variance);

        if (stdDev < 0.0001)
        {
            var p = mean / 100.0;
            var avgN = baselineDenominators.Average();
            stdDev = Math.Sqrt(Math.Max(p * (1.0 - p), 0.01) / Math.Max(avgN, 1.0)) * 100.0;
        }

        var ucl = Math.Min(100.0, mean + 2.0 * stdDev);
        if (currentRate > ucl && currentRate > mean + 2.0)
        {
            var zScore = stdDev > 0 ? (currentRate - mean) / stdDev : 0;
            var delta = currentRate - mean;
            var currentWindowLabel = FormatPeriodLabel(currentStart, currentEnd);

            alerts.Add(new()
            {
                MetricType = metricType,
                MetricCode = metricCode,
                MetricName = metricName,
                OrganismLabel = organismLabel,
                CurrentWindowLabel = currentWindowLabel,
                CurrentResistant = currRes,
                CurrentTested = currTested,
                CurrentRate = Math.Round(currentRate, 1),
                BaselineMeanRate = Math.Round(mean, 1),
                BaselineStdDev = Math.Round(stdDev, 1),
                UpperControlLimit = Math.Round(ucl, 1),
                BaselinePeriodCount = baselineRates.Count,
                DeltaPercentagePoints = Math.Round(delta, 1),
                ZScore = Math.Round(zScore, 2),
                SummaryMessage =
                    $"{organismLabel} — {metricName} reached {currentRate:F1}% ({currRes}/{currTested}) in {currentWindowLabel}, exceeding the 5-cycle upper control limit of {ucl:F1}% (baseline mean {mean:F1}%)."
            });
        }
    }

    private async Task<Dictionary<string, Dictionary<string, string>>> LoadASTInterpretationsAsync(
        List<string> isolateIds,
        List<Antibiotic> antibiotics,
        CancellationToken cancellationToken)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.OrdinalIgnoreCase);
        if (isolateIds.Count == 0 || antibiotics.Count == 0)
            return result;

        var validCodes = antibiotics
            .Select(a => NormalizeCode(a.AntibioticCode))
            .Where(c => c != null)
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validCodes.Count == 0)
            return result;

        HashSet<string> existingCols = new(StringComparer.OrdinalIgnoreCase);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);

        try
        {
            await using (var colCmd = connection.CreateCommand())
            {
                colCmd.CommandText = """
                    SELECT COLUMN_NAME
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = 'dbo'
                      AND TABLE_NAME = 'Worksheet'
                      AND COLUMN_NAME LIKE '%[_]INT';
                    """;
                await using var colReader = await colCmd.ExecuteReaderAsync(cancellationToken);
                while (await colReader.ReadAsync(cancellationToken))
                {
                    existingCols.Add(colReader.GetString(0));
                }
            }

            var selectableCodes = validCodes
                .Where(c => existingCols.Contains($"{c}_INT"))
                .ToList();

            if (selectableCodes.Count == 0)
                return result;

            var selectExprs = string.Join(", ", selectableCodes.Select(c => $"[{c}_INT]"));

            foreach (var batch in isolateIds.Chunk(500))
            {
                await using var cmd = connection.CreateCommand();
                List<string> paramNames = [];
                for (int i = 0; i < batch.Length; i++)
                {
                    var pName = $"@id{i}";
                    paramNames.Add(pName);
                    var p = cmd.CreateParameter();
                    p.ParameterName = pName;
                    p.Value = batch[i];
                    cmd.Parameters.Add(p);
                }

                cmd.CommandText = $"""
                    SELECT [IsolateID], {selectExprs}
                    FROM [dbo].[Worksheet]
                    WHERE [IsolateID] IN ({string.Join(", ", paramNames)});
                    """;

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var isoId = reader.GetString(0);
                    Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < selectableCodes.Count; i++)
                    {
                        if (!reader.IsDBNull(i + 1))
                        {
                            var val = reader.GetString(i + 1)?.Trim();
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                map[selectableCodes[i]] = val;
                            }
                        }
                    }
                    result[isoId] = map;
                }
            }
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }

        return result;
    }

    private static List<IsolateFlatRecord> FlattenGrowthIsolates(
        List<Patient> patients,
        Dictionary<string, string> facilityNameMap,
        List<string> organismFilter)
    {
        var orgSet = organismFilter.Count > 0
            ? new HashSet<string>(organismFilter, StringComparer.OrdinalIgnoreCase)
            : null;

        List<IsolateFlatRecord> list = [];

        foreach (var p in patients)
        {
            if (p.Worksheets == null || p.Worksheets.Count == 0)
            {
                continue;
            }

            var rDate = p.ReceivingDate!.Value.Date;

            foreach (var w in p.Worksheets)
            {
                // Isolates require CultureResult == "G" or "Growth"
                if (!IsGrowthResult(w.CultureResult))
                {
                    continue;
                }

                // ESKAPE+ and organism identity rely strictly on WHONETID
                var whonetId = w.WHONETID?.Trim();

                if (orgSet != null && (string.IsNullOrWhiteSpace(whonetId) || !orgSet.Contains(whonetId)))
                {
                    continue;
                }

                list.Add(new()
                {
                    PatientID = p.PatientID,
                    IsolateID = w.IsolateID,
                    FacilityID = p.FacilityID,
                    FacilityName = facilityNameMap.GetValueOrDefault(p.FacilityID, p.FacilityID),
                    ReceivingDate = rDate,
                    IsPassive = p.IsISOCase,
                    HAICAI = p.HAICAI,
                    WHONETID = whonetId,
                    IsESKAPEE = !string.IsNullOrWhiteSpace(whonetId) && ESKAPEEOrganisms.Contains(whonetId),
                    IsGramNegativeESKAPEE = !string.IsNullOrWhiteSpace(whonetId) && GramNegativeESKAPEE.Contains(whonetId),
                    MDR = w.MDR,
                    MRSA = w.MRSA,
                    Carbapenemase = w.Carbapenemase,
                    ESBL = w.ESBL
                });
            }
        }

        return list;
    }

    private static bool IsGrowthResult(string? cultureResult)
    {
        var val = cultureResult?.Trim();
        return string.Equals(val, "G", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "Growth", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractActiveParticipantKey(string patientId)
    {
        var trimmed = patientId.Trim();
        var lastDash = trimmed.LastIndexOf('-');
        if (lastDash > 0 && lastDash < trimmed.Length - 1)
        {
            var suffix = trimmed[(lastDash + 1)..];
            if (suffix.Length == 1 && char.IsDigit(suffix[0]))
            {
                return trimmed[..lastDash];
            }
        }
        return trimmed;
    }

    private static string NormalizeHAICAIFilter(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "HAI" => "HAI",
            "CAI" => "CAI",
            _ => "All"
        };

    private static bool TryParsePeriodKey(string key, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;
        var parts = key.Trim().Split('_', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 &&
               DateTime.TryParseExact(parts[0].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out start) &&
               DateTime.TryParseExact(parts[1].Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out end) &&
               end >= start;
    }

    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var cleaned = code.Trim().ToUpperInvariant();
        return ValidCodeRegex().IsMatch(cleaned) ? cleaned : null;
    }

    private sealed class IsolateFlatRecord
    {
        public string PatientID { get; set; } = string.Empty;
        public string IsolateID { get; set; } = string.Empty;
        public string FacilityID { get; set; } = string.Empty;
        public string FacilityName { get; set; } = string.Empty;
        public DateTime ReceivingDate { get; set; }
        public bool IsPassive { get; set; }
        public string? HAICAI { get; set; }
        public string? WHONETID { get; set; }
        public bool IsESKAPEE { get; set; }
        public bool IsGramNegativeESKAPEE { get; set; }
        public bool? MDR { get; set; }
        public bool? MRSA { get; set; }
        public bool? Carbapenemase { get; set; }
        public string? ESBL { get; set; }
    }
}