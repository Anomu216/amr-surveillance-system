using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class IntrinsicResistanceController : Controller
{
    private readonly IIntrinsicResistanceService _service;
    private readonly ILookupService _lookupService;

    private const string ViewPath =
        "~/Views/ReferenceData/IntrinsicResistance/";


    // =============================================
    // Constructor
    // =============================================

    public IntrinsicResistanceController(
        IIntrinsicResistanceService service,
        ILookupService lookupService)
    {
        _service = service;
        _lookupService = lookupService;
    }


    // =============================================
    // Index
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? whonetID,
        string? antibioticCode,
        string? search)
    {
        var intrinsicResistances =
            await _service.SearchAsync(
                whonetID,
                antibioticCode,
                search);

        ViewBag.SelectedOrganism = whonetID;
        ViewBag.SelectedAntibiotic = antibioticCode;
        ViewBag.Search = search;

        var model = intrinsicResistances
            .Select(r => new IntrinsicResistanceViewModel
            {
                WHONETID = r.WHONETID,
                AntibioticCode = r.AntibioticCode,
                AntibioticName =
                    r.Antibiotic?.AntibioticName
            })
            .ToList();

        return View(
            $"{ViewPath}Index.cshtml",
            model);
    }


    // =============================================
    // Create - GET
    // =============================================

    [HttpGet]
    public async Task<IActionResult> Create(
        string? whonetID,
        string? antibioticCode)
    {
        var model = new IntrinsicResistanceViewModel
        {
            WHONETID = whonetID ?? string.Empty,
            AntibioticCode = antibioticCode ?? string.Empty
        };

        await PopulateLookups(model);

        return View(
            $"{ViewPath}Create.cshtml",
            model);
    }


    // =============================================
    // Create - POST
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        IntrinsicResistanceViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLookups(model);

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }

        // ---------------------------------------------
        // Validate organism
        // ---------------------------------------------

        if (!await _service.OrganismExistsAsync(
                model.WHONETID))
        {
            ModelState.AddModelError(
                nameof(model.WHONETID),
                "The selected organism does not exist.");

            await PopulateLookups(model);

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }


        // ---------------------------------------------
        // Validate antibiotic
        // ---------------------------------------------

        if (!await _service.AntibioticExistsAsync(
                model.AntibioticCode))
        {
            ModelState.AddModelError(
                nameof(model.AntibioticCode),
                "The selected antibiotic does not exist.");

            await PopulateLookups(model);

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }


        // ---------------------------------------------
        // Check duplicate rule
        // ---------------------------------------------

        if (await _service.ExistsAsync(
            model.WHONETID,
            model.AntibioticCode))
        {
            ModelState.AddModelError(
                "",
                "This intrinsic resistance rule already exists.");

            await PopulateLookups(model);

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }


        // ---------------------------------------------
        // Create Entity
        // ---------------------------------------------

        var intrinsicResistance =
            new IntrinsicResistance
            {
                WHONETID = model.WHONETID.Trim(),
                AntibioticCode = model.AntibioticCode.Trim()
            };

        await _service.CreateAsync(
            intrinsicResistance);

        TempData["Success"] =
            "Intrinsic resistance rule created successfully.";

        return RedirectToAction(
            nameof(Index),
            new
            {
                whonetID = model.WHONETID
            });
    }


    // =============================================
    // Delete
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string whonetID,
        string antibioticCode)
    {
        if (string.IsNullOrWhiteSpace(whonetID) ||
            string.IsNullOrWhiteSpace(antibioticCode))
        {
            return NotFound();
        }

        var intrinsicResistance =
            await _service.GetByIdAsync(
                whonetID,
                antibioticCode);

        if (intrinsicResistance == null)
        {
            TempData["Error"] =
                "Intrinsic resistance rule was not found.";

            return RedirectToAction(nameof(Index));
        }

        await _service.DeleteAsync(
            whonetID,
            antibioticCode);

        TempData["Success"] =
            "Intrinsic resistance rule deleted successfully.";

        return RedirectToAction(
            nameof(Index),
            new
            {
                whonetID
            });
    }


    // =============================================
    // Excel Template
    // =============================================

    [HttpGet]
    public IActionResult Template()
    {
        using var workbook = new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add(
                "Intrinsic Resistance");

        worksheet.Cell(1, 1).Value = "WHONETID";
        worksheet.Cell(1, 2).Value = "AntibioticCode";

        worksheet.Range(1, 1, 1, 2)
            .Style.Font.Bold = true;

        worksheet.Column(1).Width = 25;
        worksheet.Column(2).Width = 25;

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "IntrinsicResistance_Template.xlsx");
    }


    // =============================================
    // Import - GET
    // =============================================

    [HttpGet]
    public IActionResult Import()
    {
        return View(
            $"{ViewPath}Import.cshtml");
    }


    // =============================================
    // Import - POST
    // =============================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] =
                "Please select an Excel file to import.";

            return RedirectToAction(nameof(Import));
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

            return RedirectToAction(nameof(Import));
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


        // ---------------------------------------------
        // Process rows
        // ---------------------------------------------

        for (var row = 2; row <= lastRow; row++)
        {
            var whonetID =
                worksheet.Cell(row, 1)
                    .GetString()
                    .Trim();

            var antibioticCode =
                worksheet.Cell(row, 2)
                    .GetString()
                    .Trim();


            // -----------------------------------------
            // Required fields
            // -----------------------------------------

            if (string.IsNullOrWhiteSpace(whonetID) ||
                string.IsNullOrWhiteSpace(antibioticCode))
            {
                skipped++;
                continue;
            }


            // -----------------------------------------
            // Check organism
            // -----------------------------------------

            if (!await _service.OrganismExistsAsync(
                    whonetID))
            {
                skipped++;
                continue;
            }


            // -----------------------------------------
            // Check antibiotic
            // -----------------------------------------

            if (!await _service.AntibioticExistsAsync(
                    antibioticCode))
            {
                skipped++;
                continue;
            }


            // -----------------------------------------
            // Check duplicate
            // -----------------------------------------

            if (await _service.ExistsAsync(
                    whonetID,
                    antibioticCode))
            {
                skipped++;
                continue;
            }


            // -----------------------------------------
            // Create relationship
            // -----------------------------------------

            var intrinsicResistance =
                new IntrinsicResistance
                {
                    WHONETID = whonetID,
                    AntibioticCode = antibioticCode
                };

            await _service.CreateAsync(
                intrinsicResistance);

            imported++;
        }


        TempData["Success"] =
            $"Import completed. {imported} relationship(s) imported and {skipped} row(s) skipped.";

        return RedirectToAction(nameof(Index));
    }


    // =============================================
    // Lookup Lists
    // =============================================

    private async Task PopulateLookups(
        IntrinsicResistanceViewModel model)
    {
        model.Organisms =
            await _lookupService.GetOrganismsAsync();

        model.Antibiotics =
            await _lookupService.GetAntibioticsAsync();
    }
}