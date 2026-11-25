using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.ViewModels;

public class ReportViewModel
{
    // ============================================================
    // FILTERS & REPORTING PERIOD (21st - 20th Anchored on ReceivingDate)
    // ============================================================

    public string? SelectedPeriodKey { get; set; }

    public DateTime PeriodStart { get; set; }

    public DateTime PeriodEnd { get; set; }

    public DateTime PriorPeriodStart { get; set; }

    public DateTime PriorPeriodEnd { get; set; }

    public DateTime? LatestReceivingDate { get; set; }

    public List<string> SelectedFacilityIDs { get; set; } = [];

    public List<string> SelectedOrganismIDs { get; set; } = [];

    /// "All", "HAI", or "CAI"
    public string SelectedHAICAI { get; set; } = "All";

    public const int SmallSampleThreshold = 15;

    // ============================================================
    // DROPDOWN LOOKUPS
    // ============================================================

    public List<SelectListItem> AvailablePeriods { get; set; } = [];

    public List<SelectListItem> AvailableFacilities { get; set; } = [];

    public List<SelectListItem> AvailableOrganisms { get; set; } = [];

    // ============================================================
    // PATIENT, RECOVERED ISOLATE (G) & ESKAPE+ SUMMARY
    // ============================================================

    public int TotalPatients { get; set; }

    public int PassivePatientCount { get; set; }

    public int ActivePatientCount { get; set; }

    public int TotalIsolatesRecovered { get; set; }

    public int ActiveIsolatesRecovered { get; set; }

    public int PassiveIsolatesRecovered { get; set; }

    public int TotalESKAPEERecovered { get; set; }

    public double OverallESKAPEEShareOfIsolates =>
        TotalIsolatesRecovered == 0
            ? 0
            : Math.Round((double)TotalESKAPEERecovered / TotalIsolatesRecovered * 100.0, 1);

    public int ActiveESKAPEERecovered { get; set; }

    public double ActiveESKAPEEShareOfIsolates =>
        ActiveIsolatesRecovered == 0
            ? 0
            : Math.Round((double)ActiveESKAPEERecovered / ActiveIsolatesRecovered * 100.0, 1);

    public int PassiveESKAPEERecovered { get; set; }

    public double PassiveESKAPEEShareOfIsolates =>
        PassiveIsolatesRecovered == 0
            ? 0
            : Math.Round((double)PassiveESKAPEERecovered / PassiveIsolatesRecovered * 100.0, 1);

    public int HAICount { get; set; }

    public int CAICount { get; set; }

    public int UnknownHAICAICount { get; set; }

    public List<FacilitySurveillanceRow> FacilityBreakdown { get; set; } = [];

    public List<OrganismDistributionRow> OrganismBreakdown { get; set; } = [];

    public List<OrganismDistributionRow> NotableOtherOrganisms { get; set; } = [];

    // ============================================================
    // KEY RESISTANCE INDICATORS (CURRENT VS PRIOR PERIOD)
    // ============================================================

    public MetricComparisonCard GramNegativeMDR { get; set; } = new();

    public MetricComparisonCard OverallMDR { get; set; } = new();

    public MetricComparisonCard MRSA { get; set; } = new();

    public MetricComparisonCard CRE { get; set; } = new();

    public MetricComparisonCard ESBL { get; set; } = new();

    // ============================================================
    // DYNAMIC HIGH-CONSEQUENCE ANTIBIOTICS (IncludeInMonthlyReport)
    // ============================================================

    public List<ReportableAntibioticSummary> ReportableAntibiotics { get; set; } = [];

    public int MultiReserveResistantCount { get; set; }

    public int MultiReserveResistantDenominator { get; set; }

    // ============================================================
    // 6-PERIOD LONGITUDINAL ROLLUP & SPIKE ALERTS
    // ============================================================

    public List<MonthlyRollupPeriodPoint> SixPeriodRollup { get; set; } = [];

    public List<MonthlyRollupSpikeAlert> ReportRollupSpikes { get; set; } = [];

    // ============================================================
    // AUTO-GENERATED NARRATIVE & CAVEAT NOTES
    // ============================================================

    public string GeneratedNarrative { get; set; } = string.Empty;

    public bool HasSmallSampleCaveats { get; set; }
}

public class FacilitySurveillanceRow
{
    public string FacilityID { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public int PassivePatients { get; set; }
    public int ActivePatients { get; set; }
    public int TotalPatients { get; set; }
    public int IsolatesRecovered { get; set; }
    public int ESKAPEERecovered { get; set; }
    public int MDRCount { get; set; }
    public int MRSACount { get; set; }
    public int CRECount { get; set; }
    public int ESBLCount { get; set; }
}

public class OrganismDistributionRow
{
    public string WHONETID { get; set; } = string.Empty;
    public bool IsESKAPEE { get; set; }
    public bool IsGramNegativeESKAPEE { get; set; }
    public int Count { get; set; }
    public int HAICount { get; set; }
    public int CAICount { get; set; }
    public int MDRCount { get; set; }
    public int CRECount { get; set; }
    public int ESBLCount { get; set; }
    public int MRSACount { get; set; }
}

public class MetricComparisonCard
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int Numerator { get; set; }
    public int Denominator { get; set; }
    public double Rate => Denominator == 0 ? 0 : Math.Round((double)Numerator / Denominator * 100.0, 1);
    public bool IsSmallSample => Denominator > 0 && Denominator < ReportViewModel.SmallSampleThreshold;

    public int PriorNumerator { get; set; }
    public int PriorDenominator { get; set; }
    public double PriorRate => PriorDenominator == 0 ? 0 : Math.Round((double)PriorNumerator / PriorDenominator * 100.0, 1);
    public double DeltaPercentagePoints => Math.Round(Rate - PriorRate, 1);

    public int HAINumerator { get; set; }
    public int HAIDenominator { get; set; }
    public double HAIRate => HAIDenominator == 0 ? 0 : Math.Round((double)HAINumerator / HAIDenominator * 100.0, 1);
    public bool IsHAISmallSample => HAIDenominator > 0 && HAIDenominator < ReportViewModel.SmallSampleThreshold;

    public int CAINumerator { get; set; }
    public int CAIDenominator { get; set; }
    public double CAIRate => CAIDenominator == 0 ? 0 : Math.Round((double)CAINumerator / CAIDenominator * 100.0, 1);
    public bool IsCAISmallSample => CAIDenominator > 0 && CAIDenominator < ReportViewModel.SmallSampleThreshold;

    public List<OrganismMetricBreakdown> ByOrganism { get; set; } = [];
    public List<FacilityMetricBreakdown> ByFacility { get; set; } = [];
}

public class OrganismMetricBreakdown
{
    public string Organism { get; set; } = string.Empty;
    public int Numerator { get; set; }
    public int Denominator { get; set; }
    public double Rate => Denominator == 0 ? 0 : Math.Round((double)Numerator / Denominator * 100.0, 1);
    public bool IsSmallSample => Denominator > 0 && Denominator < ReportViewModel.SmallSampleThreshold;
}

public class FacilityMetricBreakdown
{
    public string FacilityID { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public int Numerator { get; set; }
    public int Denominator { get; set; }
    public double Rate => Denominator == 0 ? 0 : Math.Round((double)Numerator / Denominator * 100.0, 1);
    public bool IsSmallSample => Denominator > 0 && Denominator < ReportViewModel.SmallSampleThreshold;
}

public class ReportableAntibioticSummary
{
    public string AntibioticCode { get; set; } = string.Empty;
    public string AntibioticName { get; set; } = string.Empty;
    public string AntibioticClassName { get; set; } = string.Empty;

    public int ResistantCount { get; set; }
    public int TestedCount { get; set; }
    public double ResistanceRate => TestedCount == 0 ? 0 : Math.Round((double)ResistantCount / TestedCount * 100.0, 1);
    public bool IsSmallSample => TestedCount > 0 && TestedCount < ReportViewModel.SmallSampleThreshold;

    public int PriorResistantCount { get; set; }
    public int PriorTestedCount { get; set; }
    public double PriorResistanceRate => PriorTestedCount == 0 ? 0 : Math.Round((double)PriorResistantCount / PriorTestedCount * 100.0, 1);

    public int HAIResistant { get; set; }
    public int HAITested { get; set; }
    public double HAIRate => HAITested == 0 ? 0 : Math.Round((double)HAIResistant / HAITested * 100.0, 1);

    public int CAIResistant { get; set; }
    public int CAITested { get; set; }
    public double CAIRate => CAITested == 0 ? 0 : Math.Round((double)CAIResistant / CAITested * 100.0, 1);

    public List<OrganismMetricBreakdown> ByOrganism { get; set; } = [];
    public List<FacilityMetricBreakdown> ByFacility { get; set; } = [];
}

public class MonthlyRollupPeriodPoint
{
    public string PeriodKey { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public int TotalPatients { get; set; }
    public int IsolatesRecovered { get; set; }
    public int ESKAPEECount { get; set; }

    public int GnMDRNumerator { get; set; }
    public int GnMDRDenominator { get; set; }
    public double GnMDRRate => GnMDRDenominator == 0 ? 0 : Math.Round((double)GnMDRNumerator / GnMDRDenominator * 100.0, 1);

    public int MRSANumerator { get; set; }
    public int MRSADenominator { get; set; }
    public double MRSARate => MRSADenominator == 0 ? 0 : Math.Round((double)MRSANumerator / MRSADenominator * 100.0, 1);

    public int CRENumerator { get; set; }
    public int CREDenominator { get; set; }
    public double CRERate => CREDenominator == 0 ? 0 : Math.Round((double)CRENumerator / CREDenominator * 100.0, 1);

    public int ESBLNumerator { get; set; }
    public int ESBLDenominator { get; set; }
    public double ESBLRate => ESBLDenominator == 0 ? 0 : Math.Round((double)ESBLNumerator / ESBLDenominator * 100.0, 1);

    public Dictionary<string, double> AntibioticRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> AntibioticDenominators { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class MonthlyRollupSpikeAlert
{
    public string MetricType { get; set; } = string.Empty;
    public string MetricCode { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string OrganismLabel { get; set; } = string.Empty;
    public string CurrentWindowLabel { get; set; } = string.Empty;

    public int CurrentResistant { get; set; }
    public int CurrentTested { get; set; }
    public double CurrentRate { get; set; }

    public double BaselineMeanRate { get; set; }
    public double BaselineStdDev { get; set; }
    public double UpperControlLimit { get; set; }
    public int BaselinePeriodCount { get; set; }

    public double DeltaPercentagePoints { get; set; }
    public double ZScore { get; set; }
    public string SummaryMessage { get; set; } = string.Empty;
}