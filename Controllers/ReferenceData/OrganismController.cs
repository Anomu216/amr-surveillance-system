using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class OrganismController : Controller
{
    private readonly IOrganismService _service;

    private const string ViewPath =
        "~/Views/ReferenceData/Organism/";

    public OrganismController(
        IOrganismService service)
    {
        _service = service;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var organisms =
            await _service.GetAllAsync();

        var model = organisms
            .Select(o => new OrganismViewModel
            {
                WHONETID = o.WHONETID,
                IntrinsicResistanceCount =
                    o.IntrinsicResistances.Count
            })
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
            new OrganismViewModel());
    }

    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        OrganismViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }

        var whonetID =
            model.WHONETID.Trim();

        if (await _service.ExistsAsync(whonetID))
        {
            ModelState.AddModelError(
                nameof(model.WHONETID),
                "An organism with this WHONET ID already exists.");

            return View(
                $"{ViewPath}Create.cshtml",
                model);
        }

        var organism = new Organism
        {
            WHONETID = whonetID
        };

        await _service.CreateAsync(organism);

        TempData["Success"] =
            "Organism created successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    // =========================================================
    // DELETE
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string whonetID)
    {
        if (string.IsNullOrWhiteSpace(whonetID))
        {
            return NotFound();
        }

        var organism =
            await _service.GetByIdAsync(
                whonetID);

        if (organism == null)
        {
            TempData["Error"] =
                "Organism was not found.";

            return RedirectToAction(
                nameof(Index));
        }

        try
        {
            await _service.DeleteAsync(
                whonetID);

            TempData["Success"] =
                "Organism deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "This organism cannot be deleted because it is already being used by one or more worksheet records.";
        }

        return RedirectToAction(
            nameof(Index));
    }

    // =========================================================
    // TEMPLATE
    // =========================================================

    [HttpGet]
    public IActionResult Template()
    {
        using var workbook = new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add("Organisms");

        worksheet.Cell(1, 1).Value =
            "WHONETID";

        worksheet.Cell(1, 1)
            .Style.Font.Bold = true;

        worksheet.Column(1).Width = 25;

        using var stream =
            new MemoryStream();

        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Organism_Template.xlsx");
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
            var whonetID =
                worksheet.Cell(row, 1)
                    .GetString()
                    .Trim();

            if (string.IsNullOrWhiteSpace(
                    whonetID))
            {
                skipped++;
                continue;
            }

            if (await _service.ExistsAsync(
                    whonetID))
            {
                skipped++;
                continue;
            }

            var organism = new Organism
            {
                WHONETID = whonetID
            };

            await _service.CreateAsync(
                organism);

            imported++;
        }

        TempData["Success"] =
            $"Import completed. {imported} organism(s) imported and {skipped} row(s) skipped.";

        return RedirectToAction(
            nameof(Index));
    }
}