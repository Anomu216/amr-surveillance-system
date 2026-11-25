namespace AMRSurveillance.ViewModels;

/// Contains aggregated summary data only for dashboard exports.
public class DashboardExportViewModel
{
    public DashboardViewModel Summary { get; set; } = new();

    public List<ExportSummaryRow> PatientSummaries { get; set; } = [];

    public List<ExportSummaryRow> IsolateSummaries { get; set; } = [];

    public List<ExportSummaryRow> GenomicsSummaries { get; set; } = [];
}

/// A grouped, aggregated result suitable for export.
/// Category identifies the grouping; Count and Denominator support reporting of counts and percentages.
public class ExportSummaryRow
{
    public string Category { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public int Count { get; set; }

    public int? Denominator { get; set; }

    public double? Percentage { get; set; }

    public string? Notes { get; set; }
}