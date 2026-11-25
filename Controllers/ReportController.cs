using AMRSurveillance.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

[Authorize]
public class ReportController(
    IReportService reportService,
    IAuditService auditService) : Controller
{
    private readonly IReportService _reportService = reportService;
    private readonly IAuditService _auditService = auditService;

    [HttpGet]
    public async Task<IActionResult> Index(
        string? periodKey,
        DateTime? customStart,
        DateTime? customEnd,
        List<string>? facilityIds,
        List<string>? organismIds,
        string? organism,
        string? haiCai,
        CancellationToken cancellationToken)
    {
        List<string> mergedOrganisms = organismIds is not null ? [.. organismIds] : [];
        if (!string.IsNullOrWhiteSpace(organism) &&
            !mergedOrganisms.Contains(organism.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            mergedOrganisms.Add(organism.Trim());
        }

        var model = await _reportService.GenerateMonthlyReportAsync(
            periodKey,
            customStart,
            customEnd,
            facilityIds,
            mergedOrganisms,
            haiCai,
            cancellationToken);

        var periodDesc = !string.IsNullOrWhiteSpace(periodKey)
            ? periodKey
            : (customStart.HasValue || customEnd.HasValue
                ? $"{customStart:yyyy-MM-dd} to {customEnd:yyyy-MM-dd}"
                : "Default period");

        var facilityDesc = facilityIds is { Count: > 0 }
            ? $"{facilityIds.Count} facility(ies)"
            : "All facilities";

        var organismDesc = mergedOrganisms.Count > 0
            ? string.Join(", ", mergedOrganisms)
            : "All organisms";

        var scopeDesc = string.IsNullOrWhiteSpace(haiCai) ? "All cases" : haiCai.Trim();

        await _auditService.LogAsync(
            action: "ReportGenerated",
            entityName: "MonthlyReport",
            entityKey: periodDesc,
            summary: $"Generated surveillance report ({periodDesc}; {facilityDesc}; {organismDesc}; {scopeDesc})",
            cancellationToken: cancellationToken);

        return View(model);
    }
}