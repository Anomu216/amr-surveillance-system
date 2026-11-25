
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

[Authorize(Roles = AppRoles.SystemAdministrator)]
public class FacilityController : Controller
{
    private readonly IFacilityService _service;
    private readonly IImportWorkflow<Facility> _workflow;

    public FacilityController(
        IFacilityService service,
        IImportWorkflow<Facility> workflow)
    {
        _service = service;
        _workflow = workflow;
    }

    private const string SessionKey = "FacilityImport";
    private const string TemplateFileName = "FacilityTemplate.xlsx";

    // LIST
    public async Task<IActionResult> Index()
    {
        var facilities = await _service.GetAllAsync();
        return View(facilities);
    }

    // DETAILS
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var facility = await _service.GetByIdAsync(id);

        if (facility == null)
            return NotFound();

        return View(facility);
    }

    // CREATE (GET)
    [HttpGet]
    public IActionResult Create()
    {
        return View(new FacilityViewModel());
    }

    // CREATE (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FacilityViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (await _service.ExistsAsync(model.FacilityID))
        {
            ModelState.AddModelError(
                nameof(model.FacilityID),
                "A facility with this code already exists.");

            return View(model);
        }

        var facility = new Facility
        {
            FacilityID = model.FacilityID.Trim().ToUpper(),
            FacilityName = model.FacilityName.Trim(),
            County = model.County.Trim(),
            SubCounty = model.SubCounty.Trim(),
            FacilityLevel = model.FacilityLevel.Trim(),
            Ownership = model.Ownership.Trim()
        };

        await _service.CreateAsync(facility);

        TempData["Success"] = "Facility created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // EDIT (GET)
    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var facility = await _service.GetByIdAsync(id);

        if (facility == null)
            return NotFound();

        var model = new FacilityViewModel
        {
            FacilityID = facility.FacilityID,
            FacilityName = facility.FacilityName,
            County = facility.County,
            SubCounty = facility.SubCounty,
            FacilityLevel = facility.FacilityLevel,
            Ownership = facility.Ownership
        };

        return View(model);
    }

    // EDIT (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        FacilityViewModel model)
    {
        if (id != model.FacilityID)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var facility = await _service.GetByIdAsync(id);

        if (facility == null)
            return NotFound();

        facility.FacilityName = model.FacilityName.Trim();
        facility.County = model.County.Trim();
        facility.SubCounty = model.SubCounty.Trim();
        facility.FacilityLevel = model.FacilityLevel.Trim();
        facility.Ownership = model.Ownership.Trim();

        await _service.UpdateAsync(facility);

        TempData["Success"] = "Facility updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // IMPORT (GET)
    [HttpGet]
    public IActionResult Import()
    {
        return View(new ImportViewModel<Facility>());
    }

    // IMPORT (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        ImportViewModel<Facility> model)
    {
        if (model.File == null)
        {
            ModelState.AddModelError(
                nameof(model.File),
                "Please select an Excel file.");

            return View(model);
        }

        var requiredHeaders = FacilityImportDefinition.Fields
            .Select(f => f.Name)
            .ToList();

        model = await _workflow.ValidateAsync(
            model.File,
            requiredHeaders,
            SessionKey);

        return View(model);
    }

    // CONFIRM IMPORT
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport()
    {
        var summary = await _workflow.ConfirmImportAsync(SessionKey);

        if (summary.SuccessfulRows == 0)
        {
            TempData["Error"] =
                "No valid records were available for import.";
        }
        else
        {
            TempData["Success"] =
                $"{summary.SuccessfulRows} facility record(s) imported successfully.";
        }

        return RedirectToAction(nameof(Index));
    }

    // DOWNLOAD TEMPLATE
    [HttpGet]
    public async Task<IActionResult> DownloadTemplate()
    {
        return await _workflow.DownloadTemplateAsync(
            FacilityImportDefinition.Fields,
            TemplateFileName);
    }

    // CLEAR IMPORT
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearImport()
    {
        _workflow.ClearSession(SessionKey);
        return RedirectToAction(nameof(Import));
    }

    // DELETE intentionally omitted.
    // Facilities should not be physically deleted because
    // patient records reference them.
}
