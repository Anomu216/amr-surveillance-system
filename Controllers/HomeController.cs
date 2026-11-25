using System.Globalization;
using System.Text;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

[Authorize]
public class HomeController(
    IDashboardService dashboardService,
    IAuditService auditService) : Controller
{
    private readonly IDashboardService _dashboardService = dashboardService;
    private readonly IAuditService _auditService = auditService;

    // ------------------------------------------------------------
    // DASHBOARD
    // ------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] DashboardViewModel filters,
        CancellationToken cancellationToken)
    {
        var dashboard = await _dashboardService.GetDashboardAsync(
            filters,
            cancellationToken);

        return View(dashboard);
    }

    // ------------------------------------------------------------
    // EXCEL EXPORT
    // Contains aggregated summaries only (Accepted samples).
    // ------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        [FromQuery] DashboardViewModel filters,
        CancellationToken cancellationToken)
    {
        var export = await _dashboardService.GetExportDataAsync(
            filters,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(export.Summary.ErrorMessage))
        {
            return BadRequest(export.Summary.ErrorMessage);
        }

        using XLWorkbook workbook = new();

        AddDashboardSummaryWorksheet(workbook, export.Summary);

        AddSummaryWorksheet(
            workbook,
            "Patient Summary",
            export.PatientSummaries);

        AddSummaryWorksheet(
            workbook,
            "Isolate Summary",
            export.IsolateSummaries);

        AddSummaryWorksheet(
            workbook,
            "Genomics Summary",
            export.GenomicsSummaries);

        using MemoryStream stream = new();

        workbook.SaveAs(stream);
        var content = stream.ToArray();

        var scopeSummary = BuildFilterAuditDescription(export.Summary);

        await _auditService.LogAsync(
            action: "Export",
            entityName: "DashboardSummary",
            entityKey: "Excel",
            summary: $"Exported dashboard Excel summary ({scopeSummary})",
            cancellationToken: cancellationToken);

        var fileName =
            $"AMR_Dashboard_Summary_{EastAfricaTime.Now:yyyyMMdd_HHmmss}_EAT.xlsx";

        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    // ------------------------------------------------------------
    // CSV EXPORT
    // Exports one selected aggregated summary only (Accepted samples).
    // ------------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string dataset,
        [FromQuery] DashboardViewModel filters,
        CancellationToken cancellationToken)
    {
        var normalizedDataset = dataset?.Trim().ToLowerInvariant();

        if (normalizedDataset is not ("patients" or "isolates" or "genomics"))
        {
            return BadRequest(
                "Invalid dataset. Choose patients, isolates, or genomics.");
        }

        var export = await _dashboardService.GetExportDataAsync(
            filters,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(export.Summary.ErrorMessage))
        {
            return BadRequest(export.Summary.ErrorMessage);
        }

        List<ExportSummaryRow> rows;
        string filePrefix;

        switch (normalizedDataset)
        {
            case "patients":
                rows = export.PatientSummaries;
                filePrefix = "AMR_Patient_Summary";
                break;

            case "isolates":
                rows = export.IsolateSummaries;
                filePrefix = "AMR_Isolate_Summary";
                break;

            default:
                rows = export.GenomicsSummaries;
                filePrefix = "AMR_Genomics_Summary";
                break;
        }

        var csv = CreateSummaryCsv(rows);

        var bytes = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: true).GetBytes(csv);

        var scopeSummary = BuildFilterAuditDescription(export.Summary);

        await _auditService.LogAsync(
            action: "Export",
            entityName: "DashboardSummary",
            entityKey: $"CSV:{normalizedDataset}",
            summary: $"Exported dashboard {normalizedDataset} CSV summary ({rows.Count} rows; {scopeSummary})",
            cancellationToken: cancellationToken);

        var fileName =
            $"{filePrefix}_{EastAfricaTime.Now:yyyyMMdd_HHmmss}_EAT.csv";

        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // ------------------------------------------------------------
    // EXCEL: DASHBOARD SUMMARY WORKSHEET
    // ------------------------------------------------------------

    private static void AddDashboardSummaryWorksheet(
        XLWorkbook workbook,
        DashboardViewModel summary)
    {
        var worksheet = workbook.Worksheets.Add("Dashboard Summary");

        worksheet.Cell(1, 1).Value = "AMR Surveillance Dashboard Summary";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 16;

        worksheet.Cell(2, 1).Value = "Data updated (EAT)";
        worksheet.Cell(2, 2).Value = summary.DataUpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        worksheet.Cell(3, 1).Value = "Analysis scope";
        worksheet.Cell(3, 2).Value = "All surveillance analytics reflect accepted samples only";

        worksheet.Cell(5, 1).Value = "Metric";
        worksheet.Cell(5, 2).Value = "Value";

        (string Metric, int Value)[] kpis =
        [
            ("Accepted Samples (Total Patients)", summary.TotalPatients),
            ("Rejected Samples", summary.RejectedSamples),
            ("Total Submitted Samples", summary.TotalSubmittedSamples),
            ("Total Isolates", summary.TotalIsolates),
            ("Positive Cultures", summary.PositiveCultures),
            ("MDR Isolates", summary.MDRIsolates),
            ("Sequenced Isolates", summary.SequencedIsolates)
        ];

        var row = 6;

        foreach (var (metric, value) in kpis)
        {
            worksheet.Cell(row, 1).Value = metric;
            worksheet.Cell(row, 2).Value = value;
            row++;
        }

        row += 1;

        worksheet.Cell(row, 1).Value = "Applied Filters";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        worksheet.Cell(row, 1).Value = "Start date";
        worksheet.Cell(row++, 2).Value =
            summary.StartDate?.ToString("yyyy-MM-dd") ?? "All dates";

        worksheet.Cell(row, 1).Value = "End date";
        worksheet.Cell(row++, 2).Value =
            summary.EndDate?.ToString("yyyy-MM-dd") ?? "All dates";

        worksheet.Cell(row, 1).Value = "Recruitment type";
        worksheet.Cell(row++, 2).Value =
            string.IsNullOrWhiteSpace(summary.RecruitmentType)
                ? (summary.IsGroupedByRecruitmentType ? "All (Grouped by Active vs Passive)" : "All (Overall)")
                : summary.RecruitmentType;

        worksheet.Cell(row, 1).Value = "Facility ownership";
        worksheet.Cell(row++, 2).Value =
            string.IsNullOrWhiteSpace(summary.FacilityOwnership)
                ? "All ownership types"
                : summary.FacilityOwnership;

        worksheet.Cell(row, 1).Value = "Selected facilities";
        worksheet.Cell(row++, 2).Value =
            summary.FacilityIDs.Count == 0
                ? "All available facilities"
                : $"{summary.FacilityIDs.Count} selected";

        worksheet.Cell(row, 1).Value = "Selected organisms";
        worksheet.Cell(row++, 2).Value =
            summary.OrganismIDs.Count == 0
                ? "All available organisms"
                : $"{summary.OrganismIDs.Count} selected";

        AddFilterValue(
            worksheet, ref row, "Strain type", summary.StrainType);

        AddFilterValue(
            worksheet, ref row, "Phylogroup", summary.Phylogroup);

        AddFilterValue(
            worksheet, ref row, "Serotype", summary.Serotype);

        AddFilterValue(
            worksheet, ref row, "SCCmec type", summary.SCCmecType);

        AddFilterValue(
            worksheet, ref row, "Spa type", summary.SpaType);

        AddFilterValue(
            worksheet, ref row, "O locus", summary.OLocus);

        AddFilterValue(
            worksheet, ref row, "K locus", summary.KLocus);

        row += 1;

        worksheet.Cell(row, 1).Value = "Additional Metrics";
        worksheet.Cell(row, 1).Style.Font.Bold = true;
        row++;

        worksheet.Cell(row, 1).Value = "Negative Cultures";
        worksheet.Cell(row++, 2).Value = summary.NegativeCultures;

        worksheet.Cell(row, 1).Value = "Isolates with MDR not recorded";
        worksheet.Cell(row++, 2).Value =
            summary.IsolatesWithMDRNotRecorded;

        worksheet.Cell(row, 1).Value = "Isolates with AST";
        worksheet.Cell(row++, 2).Value = summary.IsolatesWithAST;

        worksheet.Cell(row, 1).Value = "Isolates with gene results";
        worksheet.Cell(row, 2).Value = summary.IsolatesWithGeneResults;

        FormatWorksheet(worksheet);
    }

    private static string BuildFilterAuditDescription(DashboardViewModel summary)
    {
        var recruitment = string.IsNullOrWhiteSpace(summary.RecruitmentType)
            ? (summary.IsGroupedByRecruitmentType ? "Recruitment=All (Grouped)" : "Recruitment=All")
            : $"Recruitment={summary.RecruitmentType}";

        var dateRange = summary.StartDate.HasValue || summary.EndDate.HasValue
            ? $"Dates={summary.StartDate:yyyy-MM-dd}..{summary.EndDate:yyyy-MM-dd}"
            : "Dates=All";

        var facilities = summary.FacilityIDs.Count == 0
            ? "Facilities=All"
            : $"Facilities={summary.FacilityIDs.Count}";

        var organisms = summary.OrganismIDs.Count == 0
            ? "Organisms=All"
            : $"Organisms={summary.OrganismIDs.Count}";

        return $"{recruitment}; {dateRange}; {facilities}; {organisms}";
    }

    private static void AddFilterValue(
        IXLWorksheet worksheet,
        ref int row,
        string label,
        string? value)
    {
        worksheet.Cell(row, 1).Value = label;
        worksheet.Cell(row, 2).Value =
            string.IsNullOrWhiteSpace(value) ? "All" : value;

        row++;
    }

    // ------------------------------------------------------------
    // EXCEL: AGGREGATED SUMMARY WORKSHEETS
    // ------------------------------------------------------------

    private static void AddSummaryWorksheet(
        XLWorkbook workbook,
        string worksheetName,
        IEnumerable<ExportSummaryRow> rows)
    {
        var worksheet = workbook.Worksheets.Add(worksheetName);

        worksheet.Cell(1, 1).Value = worksheetName;
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        string[] headers =
        [
            "Category",
            "Group",
            "Count",
            "Denominator",
            "Percentage",
            "Notes"
        ];

        for (var column = 0; column < headers.Length; column++)
        {
            worksheet.Cell(3, column + 1).Value = headers[column];
        }

        var rowNumber = 4;

        foreach (var item in rows)
        {
            worksheet.Cell(rowNumber, 1).Value = item.Category;
            worksheet.Cell(rowNumber, 2).Value = item.Group;
            worksheet.Cell(rowNumber, 3).Value = item.Count;

            if (item.Denominator.HasValue)
            {
                worksheet.Cell(rowNumber, 4).Value =
                    item.Denominator.Value;
            }

            if (item.Percentage.HasValue)
            {
                worksheet.Cell(rowNumber, 5).Value =
                    item.Percentage.Value / 100.0;

                worksheet.Cell(rowNumber, 5).Style.NumberFormat.Format =
                    "0.00%";
            }

            worksheet.Cell(rowNumber, 6).Value =
                item.Notes ?? string.Empty;

            rowNumber++;
        }

        FormatWorksheet(worksheet);
    }

    // ------------------------------------------------------------
    // CSV: AGGREGATED SUMMARY ONLY
    // ------------------------------------------------------------

    private static string CreateSummaryCsv(
        IEnumerable<ExportSummaryRow> rows)
    {
        StringBuilder builder = new();

        builder.AppendLine(
            "Category,Group,Count,Denominator,Percentage,Notes");

        foreach (var item in rows)
        {
            string[] fields =
            [
                item.Category,
                item.Group,
                item.Count.ToString(CultureInfo.InvariantCulture),
                item.Denominator?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty,
                item.Percentage?.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture) ?? string.Empty,
                item.Notes ?? string.Empty
            ];

            builder.AppendLine(
                string.Join(",", fields.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;

        // Quote fields containing CSV special characters.
        // Also neutralize spreadsheet formula injection for text fields.
        if (value.Length > 0 &&
            (value[0] == '=' ||
             value[0] == '+' ||
             value[0] == '-' ||
             value[0] == '@' ||
             value[0] == '\t' ||
             value[0] == '\r'))
        {
            value = "'" + value;
        }

        if (value.Contains('"') ||
            value.Contains(',') ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            value = "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    // ------------------------------------------------------------
    // WORKSHEET FORMATTING
    // ------------------------------------------------------------

    private static void FormatWorksheet(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return;
        }

        // Style the relevant header row.
        var headerRow = worksheet.Name == "Dashboard Summary"
            ? 5
            : 3;

        worksheet.SheetView.FreezeRows(
            Math.Min(headerRow, usedRange.RangeAddress.LastAddress.RowNumber));

        if (usedRange.RangeAddress.LastAddress.RowNumber >= headerRow)
        {
            worksheet.Row(headerRow).Style.Font.Bold = true;
        }

        worksheet.Columns().AdjustToContents();
    }
}