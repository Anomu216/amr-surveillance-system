using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class AntibioticClassController : Controller
{
    private readonly IAntibioticClassService _service;
    private readonly IAntibioticService _antibioticService;

    private const string ViewPath =
        "~/Views/ReferenceData/AntibioticClass/";

    public AntibioticClassController(
        IAntibioticClassService service,
        IAntibioticService antibioticService)
    {
        _service = service;
        _antibioticService = antibioticService;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var classes =
            await _service.GetAllAsync();

        var antibiotics =
            await _antibioticService.GetAllAsync();

        var model = classes
            .Select(c => new AntibioticClassViewModel
            {
                AntibioticClassName =
                    c.AntibioticClassName,

                AntibioticCount =
                    antibiotics.Count(a =>
                        a.AntibioticClassName ==
                        c.AntibioticClassName)
            })
            .OrderBy(c => c.AntibioticClassName)
            .ToList();

        return View(
            $"{ViewPath}Index.cshtml",
            model);
    }

    // =========================================================
    // CREATE - GET
    // =========================================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            $"{ViewPath}Create.cshtml",
            new AntibioticClassViewModel());
    }

    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AntibioticClassViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }

        var className =
            model.AntibioticClassName.Trim();

        if (await _service.ExistsAsync(className))
        {
            ModelState.AddModelError(
                nameof(model.AntibioticClassName),
                "This antibiotic class already exists.");

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }

        var antibioticClass = new AntibioticClass
        {
            AntibioticClassName = className
        };

        await _service.CreateAsync(
            antibioticClass);

        TempData["Success"] =
            "Antibiotic class created successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    // =========================================================
    // EDIT - GET
    // =========================================================

    [HttpGet("AntibioticClass/Edit/{**id}")]
    public async Task<IActionResult> Edit(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        id = Uri.UnescapeDataString(id);

        var antibioticClass =
            await _service.GetByIdAsync(id);

        if (antibioticClass == null)
        {
            return NotFound();
        }

        var model = new AntibioticClassViewModel
        {
            AntibioticClassName =
                antibioticClass.AntibioticClassName
        };

        return View(
            $"{ViewPath}Edit.cshtml",
            model);
    }

    // =========================================================
    // EDIT - POST
    // =========================================================

    [HttpPost("AntibioticClass/Edit/{**id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        AntibioticClassViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        id = Uri.UnescapeDataString(id);

        if (!ModelState.IsValid)
        {
            return View(
                $"{ViewPath}Edit.cshtml",
                model);
        }

        var existing =
            await _service.GetByIdAsync(id);

        if (existing == null)
        {
            return NotFound();
        }

        // AntibioticClassName is the primary key.
        // It cannot be changed safely.
        if (!string.Equals(
                id,
                model.AntibioticClassName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] =
                "The antibiotic class name cannot be changed because it is the primary key.";

            return RedirectToAction(
                nameof(Index));
        }

        TempData["Success"] =
            "Antibiotic class updated successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    // =========================================================
    // DELETE
    // =========================================================

    [HttpPost("AntibioticClass/Delete/{**id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        id = Uri.UnescapeDataString(id);

        try
        {
            await _service.DeleteAsync(id);

            TempData["Success"] =
                "Antibiotic class deleted successfully.";
        }
        catch
        {
            TempData["Error"] =
                "This antibiotic class cannot be deleted because it is being used by one or more antibiotics.";
        }

        return RedirectToAction(
            nameof(Index));
    }

    // =========================================================
    // EXCEL TEMPLATE
    // =========================================================

    [HttpGet]
    public IActionResult Template()
    {
        using var workbook =
            new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add(
                "Antibiotic Categories");

        worksheet.Cell(1, 1).Value =
            "AntibioticClassName";

        worksheet.Range(1, 1, 1, 1)
            .Style.Font.Bold = true;

        worksheet.Column(1).Width = 35;

        using var stream =
            new MemoryStream();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "AntibioticClass_Template.xlsx");
    }

    // =========================================================
    // IMPORT - GET
    // =========================================================

    [HttpGet]
    public IActionResult Import()
    {
        return View(
            $"{ViewPath}Import.cshtml");
    }

    // =========================================================
    // IMPORT - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] =
                "Please select an Excel file to import.";

            return RedirectToAction(
                nameof(Import));
        }

        var extension =
            Path.GetExtension(file.FileName);

        if (!string.Equals(
                extension,
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] =
                "Only Excel (.xlsx) files are supported.";

            return RedirectToAction(
                nameof(Import));
        }

        var imported = 0;
        var skipped = 0;

        using var stream =
            file.OpenReadStream();

        using var workbook =
            new XLWorkbook(stream);

        var worksheet =
            workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            TempData["Error"] =
                "The uploaded workbook does not contain a worksheet.";

            return RedirectToAction(
                nameof(Import));
        }

        var lastRow =
            worksheet.LastRowUsed()?.RowNumber() ?? 0;

        if (lastRow < 2)
        {
            TempData["Error"] =
                "The uploaded file does not contain any data.";

            return RedirectToAction(
                nameof(Import));
        }

        for (var row = 2;
             row <= lastRow;
             row++)
        {
            var className =
                worksheet.Cell(row, 1)
                    .GetString()
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    className))
            {
                skipped++;
                continue;
            }

            if (await _service.ExistsAsync(
                    className))
            {
                skipped++;
                continue;
            }

            var antibioticClass =
                new AntibioticClass
                {
                    AntibioticClassName =
                        className
                };

            await _service.CreateAsync(
                antibioticClass);

            imported++;
        }

        TempData["Success"] =
            $"Import completed. {imported} antibiotic class(es) imported and {skipped} row(s) skipped.";

        return RedirectToAction(
            nameof(Index));
    }
}