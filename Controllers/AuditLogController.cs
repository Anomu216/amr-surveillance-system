using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers;

[Authorize(Roles = AppRoles.SystemAdministrator)]
public class AuditLogController(
    IAuditService auditService,
    ApplicationDbContext dbContext) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? startDate,
        DateTime? endDate,
        string? entityName,
        string? action,
        string? userQuery,
        string? entityKeyQuery,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 10_000);

        var (items, totalCount) = await auditService.SearchAsync(
            startDate,
            endDate,
            entityName,
            action,
            userQuery,
            entityKeyQuery,
            safePage,
            safePageSize,
            cancellationToken);

        var distinctEntities = await dbContext.AuditLogs
            .AsNoTracking()
            .Select(x => x.EntityName)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        var distinctActions = await dbContext.AuditLogs
            .AsNoTracking()
            .Select(x => x.Action)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);

        AuditLogIndexViewModel model = new()
        {
            StartDate = startDate,
            EndDate = endDate,
            EntityName = entityName,
            Action = action,
            UserQuery = userQuery,
            EntityKeyQuery = entityKeyQuery,
            Page = safePage,
            PageSize = safePageSize,
            TotalCount = totalCount,
            Items = items,
            AvailableEntities =
            [
                .. distinctEntities.Select(e => new SelectListItem
                {
                    Value = e,
                    Text = e,
                    Selected = string.Equals(e, entityName, StringComparison.OrdinalIgnoreCase)
                })
            ],
            AvailableActions =
            [
                .. distinctActions.Select(a => new SelectListItem
                {
                    Value = a,
                    Text = a,
                    Selected = string.Equals(a, action, StringComparison.OrdinalIgnoreCase)
                })
            ]
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(
        DateTime? startDate,
        DateTime? endDate,
        string? entityName,
        string? action,
        string? userQuery,
        string? entityKeyQuery,
        CancellationToken cancellationToken = default)
    {
        // Cap export at 10,000 most recent matching records
        var (items, totalCount) = await auditService.SearchAsync(
            startDate,
            endDate,
            entityName,
            action,
            userQuery,
            entityKeyQuery,
            page: 1,
            pageSize: 10_000,
            cancellationToken);

        using XLWorkbook workbook = new();
        var worksheet = workbook.Worksheets.Add("Audit Logs (EAT)");

        string[] headers =
        [
            "Audit ID",
            "Timestamp (EAT)",
            "User",
            "Action",
            "Entity",
            "Entity Key",
            "Summary",
            "IP Address",
            "Previous Values (JSON)",
            "New Values (JSON)"
        ];

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var item in items)
        {
            worksheet.Cell(row, 1).Value = item.AuditLogID;
            worksheet.Cell(row, 2).Value = item.TimestampEat.ToString("yyyy-MM-dd HH:mm:ss");
            worksheet.Cell(row, 3).Value = item.UserName ?? "System";
            worksheet.Cell(row, 4).Value = item.Action;
            worksheet.Cell(row, 5).Value = item.EntityName;
            worksheet.Cell(row, 6).Value = item.EntityKey ?? string.Empty;
            worksheet.Cell(row, 7).Value = item.Summary ?? string.Empty;
            worksheet.Cell(row, 8).Value = item.IpAddress ?? string.Empty;
            worksheet.Cell(row, 9).Value = item.OldValuesJson ?? string.Empty;
            worksheet.Cell(row, 10).Value = item.NewValuesJson ?? string.Empty;
            row++;
        }

        worksheet.Columns(1, 8).AdjustToContents();
        worksheet.Column(9).Width = 45;
        worksheet.Column(10).Width = 45;

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        await auditService.LogAsync(
            action: "Export",
            entityName: nameof(AuditLog),
            summary: $"Exported {items.Count} of {totalCount} audit log entries to Excel",
            cancellationToken: cancellationToken);

        var fileName = $"AMR_AuditLogs_{EastAfricaTime.Now:yyyyMMdd_HHmmss}_EAT.xlsx";
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}