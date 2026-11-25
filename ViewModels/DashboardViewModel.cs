using AMRSurveillance.Security;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class DashboardViewModel
{
    // ============================================================
    // GLOBAL FILTERS
    // ============================================================

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    // Optional recruitment strategy filter: null/empty = All, "Active", "Passive"
    public string? RecruitmentType { get; set; }

    // Opt-in toggle: false (default) = Overall view; true = Group by Active vs Passive
    public bool GroupByRecruitmentType { get; set; }

    public bool IsGroupedByRecruitmentType =>
        GroupByRecruitmentType && string.IsNullOrWhiteSpace(RecruitmentType);

    public string RecruitmentScopeLabel =>
        string.Equals(RecruitmentType, "Passive", StringComparison.OrdinalIgnoreCase)
            ? "Passive (ISO) Only"
            : string.Equals(RecruitmentType, "Active", StringComparison.OrdinalIgnoreCase)
                ? "Active Recruitment Only"
                : "All Recruitment";

    // Optional ownership filter: Public, Private, Faith-Based, Military, NGO, etc.
    public string? FacilityOwnership { get; set; }

    // Empty means all facilities within selected ownership (or all facilities)
    public List<string> FacilityIDs { get; set; } = [];

    // Empty means all organisms
    public List<string> OrganismIDs { get; set; } = [];

    // ============================================================
    // ISOLATE / AST SECTION FILTERS
    // ============================================================

    // One or more organisms selected for detailed AST analysis.
    // Defaults to global OrganismIDs when empty and global organisms are selected.
    public List<string> ASTOrganismIDs { get; set; } = [];

    // ============================================================
    // GENOMICS SECTION FILTERS
    // ============================================================

    public string? GenomicCharacteristic { get; set; }
    public string? GenomicCharacteristicValue { get; set; }

    public string? StrainType { get; set; }
    public string? Phylogroup { get; set; }
    public string? Serotype { get; set; }
    public string? SCCmecType { get; set; }
    public string? SpaType { get; set; }
    public string? OLocus { get; set; }
    public string? KLocus { get; set; }

    // ============================================================
    // DROPDOWN OPTIONS
    // ============================================================

    public List<SelectListItem> RecruitmentTypeOptions { get; set; } = [];
    public List<SelectListItem> FacilityOwnershipOptions { get; set; } = [];
    public List<SelectListItem> Facilities { get; set; } = [];
    public List<SelectListItem> Organisms { get; set; } = [];

    public List<SelectListItem> GenomicCharacteristicOptions { get; set; } = [];
    public List<SelectListItem> StrainTypes { get; set; } = [];
    public List<SelectListItem> Phylogroups { get; set; } = [];
    public List<SelectListItem> Serotypes { get; set; } = [];
    public List<SelectListItem> SCCmecTypes { get; set; } = [];
    public List<SelectListItem> SpaTypes { get; set; } = [];
    public List<SelectListItem> OLoci { get; set; } = [];
    public List<SelectListItem> KLoci { get; set; } = [];

    // ============================================================
    // TOP KPI CARDS (ACCEPTED SAMPLES COHORT + REJECTED TRACKING)
    // ============================================================

    // TotalPatients represents accepted samples only
    public int TotalPatients { get; set; }
    public int RejectedSamples { get; set; }
    public int TotalSubmittedSamples => TotalPatients + RejectedSamples;

    public int TotalIsolates { get; set; }
    public int PositiveCultures { get; set; }
    public int NegativeCultures { get; set; }
    public int MDRIsolates { get; set; }
    public int IsolatesWithMDRNotRecorded { get; set; }
    public int SequencedIsolates { get; set; }

    // Conditional MRSA KPI (shown when Staphylococcus aureus is selected)
    public bool ShowMRSAKpi { get; set; }
    public int MRSAIsolates { get; set; }
    public int MRSADenominator { get; set; }

    public double MRSAPercentage =>
        MRSADenominator == 0
            ? 0
            : Math.Round((double)MRSAIsolates / MRSADenominator * 100.0, 1);

    // Optional Active vs Passive KPI breakdown (populated when IsGroupedByRecruitmentType == true)
    public List<RecruitmentStratifiedKpi> RecruitmentStratifiedKpis { get; set; } = [];

    // ============================================================
    // DATA QUALITY & COMPLETENESS
    // ============================================================

    public int IsolatesWithAST { get; set; }
    public int IsolatesWithGeneResults { get; set; }

    public double ASTCoveragePercentage =>
        TotalIsolates == 0
            ? 0
            : Math.Round((double)IsolatesWithAST / TotalIsolates * 100.0, 2);

    public double SequencingCoveragePercentage =>
        TotalIsolates == 0
            ? 0
            : Math.Round((double)SequencedIsolates / TotalIsolates * 100.0, 2);

    // ============================================================
    // ROLLING 92-DAY DATA-DRIVEN SPIKE ALERTS
    // ============================================================

    public List<ResistanceSpikeAlert> DetectedSpikes { get; set; } = [];

    public bool HasDetectedSpikes => DetectedSpikes.Count > 0;

    // ============================================================
    // PATIENT & METADATA CHARTS
    // ============================================================

    public List<DashboardDataPoint> PatientsBySex { get; set; } = [];
    public List<DashboardDataPoint> PatientsByAgeGroup { get; set; } = [];
    public List<DashboardDataPoint> PatientsByMilitaryCivilianStatus { get; set; } = [];
    public List<DashboardDataPoint> PatientsByPatientType { get; set; } = [];
    public List<DashboardDataPoint> PatientsByHAICAI { get; set; } = [];
    public List<DashboardDataPoint> PatientsByClinicalPresentation { get; set; } = [];

    // ============================================================
    // ISOLATE CHARTS
    // ============================================================

    public List<DashboardDataPoint> IsolatesByOrganism { get; set; } = [];
    public List<DashboardDataPoint> CultureResults { get; set; } = [];

    // ============================================================
    // AST / ANTIBIOGRAM & TRENDS
    // ============================================================

    public string? ASTOrganismName { get; set; }
    public int ASTOrganismIsolateCount { get; set; }

    // >= 30 pooled isolates: display a percentage antibiogram.
    // < 30 pooled isolates: display a count-based AST summary.
    public bool UseAntibiogram =>
        ASTOrganismIsolateCount >= 30;

    public bool HasASTOrganismSelection =>
        ASTOrganismIDs.Count > 0;

    public bool IsMultiOrganismAST =>
        ASTOrganismIDs.Count > 1;

    public string ASTOrganismDisplayTitle =>
        ASTOrganismIDs.Count switch
        {
            0 => "No organism selected",
            1 => ASTOrganismName ?? ASTOrganismIDs[0],
            <= 3 => string.Join(", ", ASTOrganismIDs),
            _ => $"{string.Join(", ", ASTOrganismIDs.Take(2))} +{ASTOrganismIDs.Count - 2} more"
        };

    public List<AntibiogramRow> Antibiogram { get; set; } = [];
    public List<ASTHeatmapCell> ASTHeatmap { get; set; } = [];
    public List<DashboardDataPoint> AntibioticSusceptibility { get; set; } = [];

    // Time-series observations for interactive antibiotic resistance trends
    public List<AntibioticTrendObservation> AntibioticTrendObservations { get; set; } = [];

    public ResistanceMarkerSummary ASTResistanceMarkers { get; set; } = new();

    public bool HasASTData =>
        Antibiogram.Count > 0 || ASTHeatmap.Count > 0;

    // ============================================================
    // GENOMICS CHARTS
    // ============================================================

    public List<DashboardDataPoint> SequencingOverTime { get; set; } = [];
    public List<DashboardDataPoint> GenomeCompleteness { get; set; } = [];
    public List<DashboardDataPoint> StrainTypesData { get; set; } = [];
    public List<DashboardDataPoint> GenePrevalence { get; set; } = [];
    public List<DashboardDataPoint> GenomicScores { get; set; } = [];

    // ============================================================
    // STATE & METADATA
    // ============================================================

    public DateTime DataUpdatedAt { get; set; } = EastAfricaTime.Now;
    public string? ErrorMessage { get; set; }

    public bool HasData =>
        TotalPatients > 0 ||
        TotalIsolates > 0 ||
        SequencedIsolates > 0;
}

// ============================================================
// RECRUITMENT STRATIFIED KPI SUMMARY (ACTIVE VS PASSIVE)
// ============================================================

public class RecruitmentStratifiedKpi
{
    public string RecruitmentType { get; set; } = string.Empty; // "Active", "Passive", etc.
    public int TotalPatients { get; set; }
    public int RejectedSamples { get; set; }
    public int TotalIsolates { get; set; }
    public int PositiveCultures { get; set; }
    public int NegativeCultures { get; set; }
    public int MDRIsolates { get; set; }
    public int SequencedIsolates { get; set; }
    public int MRSAIsolates { get; set; }
    public int MRSADenominator { get; set; }

    public double MRSAPercentage =>
        MRSADenominator > 0
            ? Math.Round((double)MRSAIsolates / MRSADenominator * 100.0, 1)
            : 0.0;
}

// ============================================================
// CHART DATA POINT
// ============================================================

public class DashboardDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }

    // Optional stacked or multi-series values (e.g. I%, R%, counts)
    public double? SecondaryValue { get; set; }
    public double? TertiaryValue { get; set; }

    public int? Denominator { get; set; }
    public string? Category { get; set; }
}

// ============================================================
// ANTIBIOGRAM ROW (N >= 30)
// ============================================================

public class AntibiogramRow
{
    public string AntibioticCode { get; set; } = string.Empty;
    public string AntibioticName { get; set; } = string.Empty;

    // Optional stratification label when grouped by RecruitmentType (e.g., "Active", "Passive")
    public string? RecruitmentType { get; set; }

    public int Tested { get; set; }
    public int Susceptible { get; set; }
    public int Intermediate { get; set; }
    public int Resistant { get; set; }

    public double SusceptiblePercentage =>
        Tested == 0
            ? 0
            : Math.Round((double)Susceptible / Tested * 100.0, 2);

    public double IntermediatePercentage =>
        Tested == 0
            ? 0
            : Math.Round((double)Intermediate / Tested * 100.0, 2);

    public double ResistantPercentage =>
        Tested == 0
            ? 0
            : Math.Round((double)Resistant / Tested * 100.0, 2);
}

// ============================================================
// ANTIBIOTIC RESISTANCE TREND OBSERVATION (MONTHLY BUCKET)
// ============================================================

public class AntibioticTrendObservation
{
    public string AntibioticCode { get; set; } = string.Empty;
    public string AntibioticName { get; set; } = string.Empty;

    // Optional stratification label when grouped by RecruitmentType (e.g., "Active", "Passive")
    public string? RecruitmentType { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }
    public int Tested { get; set; }
    public int Resistant { get; set; }
    public int Susceptible { get; set; }
}

// ============================================================
// ROLLING 92-DAY DATA-DRIVEN RESISTANCE SPIKE ALERT
// ============================================================

public class ResistanceSpikeAlert
{
    public string MetricType { get; set; } = string.Empty; // "MDR", "MRSA", "Antibiotic"
    public string MetricCode { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string OrganismLabel { get; set; } = string.Empty;

    public DateTime CurrentWindowStart { get; set; }
    public DateTime CurrentWindowEnd { get; set; }
    public int CurrentNumerator { get; set; }
    public int CurrentDenominator { get; set; }

    public double CurrentPercentage =>
        CurrentDenominator == 0
            ? 0
            : Math.Round((double)CurrentNumerator / CurrentDenominator * 100.0, 2);

    // Data-driven historical baseline (preceding 92-day blocks with N >= 30)
    public int BaselinePeriodsCount { get; set; }
    public double HistoricalMean { get; set; }
    public double HistoricalStdDev { get; set; }
    public double UpperControlLimit { get; set; }
    public double ZScore { get; set; }

    public double DeltaFromMean =>
        Math.Round(CurrentPercentage - HistoricalMean, 2);

    public string SummaryMessage =>
        $"{MetricName} ({OrganismLabel}) reached {CurrentPercentage:F1}% ({CurrentNumerator}/{CurrentDenominator}) " +
        $"over the last 92 days ({CurrentWindowStart:dd MMM}–{CurrentWindowEnd:dd MMM yyyy}), exceeding its historical control limit of " +
        $"{UpperControlLimit:F1}% (historical mean {HistoricalMean:F1}% ± {HistoricalStdDev:F1}% across {BaselinePeriodsCount} prior 92-day periods; z = {ZScore:F2}).";
}

// ============================================================
// AST HEATMAP CELL (N < 30, COUNTS ONLY)
// ============================================================

public class ASTHeatmapCell
{
    public string IsolateID { get; set; } = string.Empty;
    public string AntibioticCode { get; set; } = string.Empty;
    public string AntibioticName { get; set; } = string.Empty;

    // S, I, R, or NT
    public string Result { get; set; } = "NT";
}

// ============================================================
// ORGANISM-SPECIFIC RESISTANCE SUMMARY
// ============================================================

public class ResistanceMarkerSummary
{
    public int MDRCount { get; set; }
    public int MDRDenominator { get; set; }

    public int ESBLCount { get; set; }
    public int ESBLDenominator { get; set; }

    public int CarbapenemaseCount { get; set; }
    public int CarbapenemaseDenominator { get; set; }

    public int MRSACount { get; set; }
    public int MRSADenominator { get; set; }
}