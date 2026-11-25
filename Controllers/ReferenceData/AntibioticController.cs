using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class AntibioticController(
    IAntibioticService service,
    IAntibioticClassService antibioticClassService) : Controller
{
    private readonly IAntibioticService _service = service;
    private readonly IAntibioticClassService _antibioticClassService = antibioticClassService;

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(string? antibioticClass)
    {
        var antibiotics = await _service.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(antibioticClass))
        {
            antibiotics =
            [
                .. antibiotics.Where(a => a.AntibioticClassName == antibioticClass)
            ];
        }

        var model = antibiotics.Select(a => new AntibioticViewModel
        {
            AntibioticCode = a.AntibioticCode,
            AntibioticName = a.AntibioticName,
            AntibioticClassName = a.AntibioticClassName,
            ASTCard = a.ASTCard,
            MICEnabled = a.MICEnabled,
            InterpretationEnabled = a.InterpretationEnabled,
            Active = a.Active,
            IncludeInMonthlyReport = a.IncludeInMonthlyReport
        });

        ViewBag.SelectedClass = antibioticClass;

        return View(
            "~/Views/ReferenceData/Antibiotic/Index.cshtml",
            model);
    }

    // =========================================================
    // CREATE - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        AntibioticViewModel model = new();

        await LoadAntibioticClassesAsync(model);

        return View(
            "~/Views/ReferenceData/Antibiotic/Create.cshtml",
            model);
    }

    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AntibioticViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadAntibioticClassesAsync(model);

            return View(
                "~/Views/ReferenceData/Antibiotic/Create.cshtml",
                model);
        }

        var code = model.AntibioticCode.Trim();

        if (await _service.ExistsAsync(code))
        {
            ModelState.AddModelError(
                nameof(model.AntibioticCode),
                "This antibiotic code already exists.");

            await LoadAntibioticClassesAsync(model);

            return View(
                "~/Views/ReferenceData/Antibiotic/Create.cshtml",
                model);
        }

        Antibiotic antibiotic = new()
        {
            AntibioticCode = code,
            AntibioticName = model.AntibioticName.Trim(),
            AntibioticClassName = model.AntibioticClassName,
            ASTCard = model.ASTCard?.Trim() ?? string.Empty,
            MICEnabled = model.MICEnabled,
            InterpretationEnabled = model.InterpretationEnabled,
            Active = model.Active,
            IncludeInMonthlyReport = model.IncludeInMonthlyReport
        };

        await _service.CreateAsync(antibiotic);

        TempData["Success"] = "Antibiotic created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // EDIT - GET
    // =========================================================

    [HttpGet("Antibiotic/Edit/{**id}")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id);

        var antibiotic = await _service.GetByIdAsync(id);

        if (antibiotic == null)
            return NotFound();

        AntibioticViewModel model = new()
        {
            AntibioticCode = antibiotic.AntibioticCode,
            AntibioticName = antibiotic.AntibioticName,
            AntibioticClassName = antibiotic.AntibioticClassName,
            ASTCard = antibiotic.ASTCard,
            MICEnabled = antibiotic.MICEnabled,
            InterpretationEnabled = antibiotic.InterpretationEnabled,
            Active = antibiotic.Active,
            IncludeInMonthlyReport = antibiotic.IncludeInMonthlyReport
        };

        await LoadAntibioticClassesAsync(model);

        return View(
            "~/Views/ReferenceData/Antibiotic/Edit.cshtml",
            model);
    }

    // =========================================================
    // EDIT - POST
    // =========================================================

    [HttpPost("Antibiotic/Edit/{**id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        AntibioticViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id);

        if (!ModelState.IsValid)
        {
            await LoadAntibioticClassesAsync(model);

            return View(
                "~/Views/ReferenceData/Antibiotic/Edit.cshtml",
                model);
        }

        var existing = await _service.GetByIdAsync(id);

        if (existing == null)
            return NotFound();

        // AntibioticCode is the primary key and cannot be changed.
        if (!string.Equals(
                id,
                model.AntibioticCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] =
                "The antibiotic code cannot be changed because it is the primary key.";

            return RedirectToAction(nameof(Index));
        }

        existing.AntibioticName = model.AntibioticName.Trim();
        existing.AntibioticClassName = model.AntibioticClassName;
        existing.ASTCard = model.ASTCard?.Trim() ?? string.Empty;
        existing.MICEnabled = model.MICEnabled;
        existing.InterpretationEnabled = model.InterpretationEnabled;
        existing.Active = model.Active;
        existing.IncludeInMonthlyReport = model.IncludeInMonthlyReport;

        await _service.UpdateAsync(id, existing);

        TempData["Success"] = "Antibiotic updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // DELETE
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        try
        {
            await _service.DeleteAsync(id);

            TempData["Success"] =
                "Antibiotic deleted successfully.";
        }
        catch
        {
            TempData["Error"] =
                "This antibiotic cannot be deleted because it is being used by other records.";
        }

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // EXCEL TEMPLATE
    // =========================================================

    [HttpGet]
    public IActionResult Template()
    {
        using XLWorkbook workbook = new();

        var worksheet = workbook.Worksheets.Add("Antibiotics");

        worksheet.Cell(1, 1).Value = "AntibioticCode";
        worksheet.Cell(1, 2).Value = "AntibioticName";
        worksheet.Cell(1, 3).Value = "AntibioticClassName";
        worksheet.Cell(1, 4).Value = "ASTCard";
        worksheet.Cell(1, 5).Value = "MICEnabled";
        worksheet.Cell(1, 6).Value = "InterpretationEnabled";
        worksheet.Cell(1, 7).Value = "Active";
        worksheet.Cell(1, 8).Value = "IncludeInMonthlyReport";

        worksheet.Range(1, 1, 1, 8)
            .Style.Font.Bold = true;

        worksheet.Column(1).Width = 20;
        worksheet.Column(2).Width = 35;
        worksheet.Column(3).Width = 30;
        worksheet.Column(4).Width = 30;
        worksheet.Column(5).Width = 18;
        worksheet.Column(6).Width = 25;
        worksheet.Column(7).Width = 15;
        worksheet.Column(8).Width = 24;

        using MemoryStream stream = new();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Antibiotic_Template.xlsx");
    }

    // =========================================================
    // IMPORT - GET
    // =========================================================

    [HttpGet]
    public IActionResult Import()
    {
        return View(
            "~/Views/ReferenceData/Antibiotic/Import.cshtml");
    }

    // =========================================================
    // IMPORT - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] =
                "Please select an Excel file to import.";

            return RedirectToAction(nameof(Import));
        }

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(
                extension,
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] =
                "Only Excel (.xlsx) files are supported.";

            return RedirectToAction(nameof(Import));
        }

        var imported = 0;
        var skipped = 0;

        using var stream = file.OpenReadStream();
        using XLWorkbook workbook = new(stream);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            TempData["Error"] =
                "The uploaded workbook does not contain a worksheet.";

            return RedirectToAction(nameof(Import));
        }

        var lastRow =
            worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastRow < 2)
        {
            TempData["Error"] =
                "The uploaded file does not contain any data.";

            return RedirectToAction(nameof(Import));
        }

        for (var row = 2; row <= lastRow; row++)
        {
            var code =
                worksheet.Cell(row, 1).GetString().Trim();

            var name =
                worksheet.Cell(row, 2).GetString().Trim();

            var className =
                worksheet.Cell(row, 3).GetString().Trim();

            var astCard =
                worksheet.Cell(row, 4).GetString().Trim();

            if (string.IsNullOrWhiteSpace(code) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(className))
            {
                skipped++;
                continue;
            }

            if (await _service.ExistsAsync(code))
            {
                skipped++;
                continue;
            }

            if (!await _antibioticClassService.ExistsAsync(className))
            {
                skipped++;
                continue;
            }

            var micEnabled = ParseBoolean(
                worksheet.Cell(row, 5).GetString());

            var interpretationEnabled = ParseBoolean(
                worksheet.Cell(row, 6).GetString());

            var active = ParseBoolean(
                worksheet.Cell(row, 7).GetString());

            var includeInMonthlyReport = ParseBoolean(
                worksheet.Cell(row, 8).GetString());

            Antibiotic antibiotic = new()
            {
                AntibioticCode = code,
                AntibioticName = name,
                AntibioticClassName = className,
                ASTCard = astCard,
                MICEnabled = micEnabled,
                InterpretationEnabled = interpretationEnabled,
                Active = active,
                IncludeInMonthlyReport = includeInMonthlyReport
            };

            await _service.CreateAsync(antibiotic);

            imported++;
        }

        TempData["Success"] =
            $"Import completed. {imported} antibiotic(s) imported and {skipped} row(s) skipped.";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // HELPER
    // =========================================================

    private async Task LoadAntibioticClassesAsync(
        AntibioticViewModel model)
    {
        var classes =
            await _antibioticClassService.GetAllAsync();

        model.AntibioticClasses =
        [
            .. classes
                .OrderBy(c => c.AntibioticClassName)
                .Select(c => new SelectListItem
                {
                    Value = c.AntibioticClassName,
                    Text = c.AntibioticClassName
                })
        ];
    }

    private static bool ParseBoolean(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "1" => true,
            _ => false
        };
    }
}