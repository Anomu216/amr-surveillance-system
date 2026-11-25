
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AMRSurveillance.Security;
using Microsoft.AspNetCore.Authorization;

namespace AMRSurveillance.Controllers.Genomics;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.GenomicsStaff)]
public class GenomicsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IGenomicsService _genomicsService;
    private readonly IImportWorkflow<GenomicsRecordViewModel> _importWorkflow;
    private readonly ILogger<GenomicsController> _logger;

    private const string ViewFolder = "~/Views/Genomics/Records/";
    private const string GenomicsImportSessionKey = "GenomicsImportRecords";

    public GenomicsController(
        ApplicationDbContext context,
        IGenomicsService genomicsService,
        IImportWorkflow<GenomicsRecordViewModel> importWorkflow,
        ILogger<GenomicsController> logger)
    {
        _context = context;
        _genomicsService = genomicsService;
        _importWorkflow = importWorkflow;
        _logger = logger;
    }

    // ============================================================
    // INDEX
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var records = await _genomicsService.GetAllRecordsAsync();

        return View($"{ViewFolder}Index.cshtml", records);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Details(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var model =
            await _genomicsService.GetRecordByIsolateIdAsync(id.Trim());

        if (model == null)
            return NotFound();

        await PopulateLookupsAsync(model.WHONETID);

        return View($"{ViewFolder}Details.cshtml", model);
    }

    // ============================================================
    // CREATE - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new GenomicsRecordViewModel();

        await PopulateLookupsAsync(model.WHONETID);

        return View($"{ViewFolder}Create.cshtml", model);
    }

    // ============================================================
    // CREATE - POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        GenomicsRecordViewModel model)
    {
        NormalizeModel(model);

        await ValidateRegisteredGenesAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model.WHONETID);

            return View($"{ViewFolder}Create.cshtml", model);
        }

        try
        {
            var isolateId = model.IsolateID.Trim();

            if (await _genomicsService.ExistsAsync(isolateId))
            {
                ModelState.AddModelError(
                    nameof(model.IsolateID),
                    "A Genomics record already exists for this isolate.");

                await PopulateLookupsAsync(model.WHONETID);

                return View($"{ViewFolder}Create.cshtml", model);
            }

            await _genomicsService.CreateAsync(model);

            TempData["SuccessMessage"] =
                $"Genomics record for isolate '{isolateId}' was created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create Genomics record for isolate {IsolateID}.",
                model.IsolateID);

            ModelState.AddModelError(
                string.Empty,
                "The Genomics record could not be created. Please check the data and try again.");
        }

        await PopulateLookupsAsync(model.WHONETID);

        return View($"{ViewFolder}Create.cshtml", model);
    }

    // ============================================================
    // EDIT - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var model =
            await _genomicsService.GetRecordByIsolateIdAsync(id.Trim());

        if (model == null)
            return NotFound();

        await PopulateLookupsAsync(model.WHONETID);

        return View($"{ViewFolder}Edit.cshtml", model);
    }

    // ============================================================
    // EDIT - POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        GenomicsRecordViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = id.Trim();

        NormalizeModel(model);

        if (!string.Equals(
                id,
                model.IsolateID?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.IsolateID),
                "Changing the isolate ID is not supported.");
        }

        await ValidateRegisteredGenesAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model.WHONETID);

            return View($"{ViewFolder}Edit.cshtml", model);
        }

        try
        {
            var existing =
                await _genomicsService.GetRecordByIsolateIdAsync(id);

            if (existing == null)
                return NotFound();

            await _genomicsService.UpdateAsync(id, model);

            TempData["SuccessMessage"] =
                $"Genomics record for isolate '{id}' was updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to update Genomics record for isolate {IsolateID}.",
                id);

            ModelState.AddModelError(
                string.Empty,
                "The Genomics record could not be updated. Please try again.");
        }

        await PopulateLookupsAsync(model.WHONETID);

        return View($"{ViewFolder}Edit.cshtml", model);
    }

    // ============================================================
    // DELETE - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var model =
            await _genomicsService.GetRecordByIsolateIdAsync(id.Trim());

        if (model == null)
            return NotFound();

        return View($"{ViewFolder}Delete.cshtml", model);
    }

    // ============================================================
    // DELETE - POST
    // ============================================================

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = id.Trim();

        try
        {
            var exists = await _genomicsService.ExistsAsync(id);

            if (!exists)
                return NotFound();

            await _genomicsService.DeleteAsync(id);

            TempData["SuccessMessage"] =
                $"Genomics record for isolate '{id}' was deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete Genomics record for isolate {IsolateID}.",
                id);

            TempData["ErrorMessage"] =
                "The Genomics record could not be deleted. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // IMPORT - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Import()
    {
        _importWorkflow.ClearSession(GenomicsImportSessionKey);

        var model =
            new ImportViewModel<GenomicsRecordViewModel>();

        await PopulateLookupsAsync();

        return View($"{ViewFolder}Import.cshtml", model);
    }

    // ============================================================
    // IMPORT - VALIDATE AND PREVIEW
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        ImportViewModel<GenomicsRecordViewModel> model)
    {
        if (model.File == null || model.File.Length == 0)
        {
            _importWorkflow.ClearSession(GenomicsImportSessionKey);

            model.IsValidated = true;

            model.Errors.Add(new ImportRowError
            {
                RowNumber = 0,
                ColumnName = string.Empty,
                Message = "Please select an Excel file."
            });

            await PopulateLookupsAsync();

            return View($"{ViewFolder}Import.cshtml", model);
        }

        var registeredGeneNames =
            await _genomicsService.GetRegisteredGeneNamesAsync();

        var fields =
            GenomicsImportDefinition.BuildFields(registeredGeneNames);

        var requiredHeaders = fields
            .Where(field => field.Required)
            .Select(field => field.Name)
            .ToList();

        var validatedModel = await _importWorkflow.ValidateAsync(
            model.File,
            requiredHeaders,
            GenomicsImportSessionKey);

        await PopulateLookupsAsync();

        return View($"{ViewFolder}Import.cshtml", validatedModel);
    }

    // ============================================================
    // IMPORT - CONFIRM
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport()
    {
        try
        {
            var summary =
                await _importWorkflow.ConfirmImportAsync(
                    GenomicsImportSessionKey);

            if (summary.SuccessfulRows > 0)
            {
                TempData["SuccessMessage"] =
                    $"{summary.SuccessfulRows} Genomics record(s) imported successfully.";
            }
            else
            {
                TempData["ErrorMessage"] =
                    "No Genomics records were imported. Please validate the workbook and try again.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Genomics Excel import failed.");

            TempData["ErrorMessage"] =
                "The Genomics import failed. No successful import was confirmed. Please review the workbook and try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // IMPORT - DOWNLOAD TEMPLATE
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        var registeredGeneNames =
            await _genomicsService.GetRegisteredGeneNamesAsync();

        var fields =
            GenomicsImportDefinition.BuildFields(registeredGeneNames);

        return await _importWorkflow.DownloadTemplateAsync(
            fields,
            "GenomicsImportTemplate.xlsx");
    }

    // ============================================================
    // IMPORT - CLEAR SESSION
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearImport()
    {
        _importWorkflow.ClearSession(GenomicsImportSessionKey);

        return RedirectToAction(nameof(Import));
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private async Task PopulateLookupsAsync(
        string? selectedWHONETID = null)
    {
        var organismIds = await _context.Organisms
            .AsNoTracking()
            .Where(o => o.WHONETID != null && o.WHONETID != "")
            .OrderBy(o => o.WHONETID)
            .Select(o => o.WHONETID)
            .ToListAsync();

        ViewBag.WHONETIDs = new SelectList(
            organismIds,
            selectedWHONETID);

        ViewBag.RegisteredGeneNames =
            await _genomicsService.GetRegisteredGeneNamesAsync();
    }

    private async Task ValidateRegisteredGenesAsync(
        GenomicsRecordViewModel model)
    {
        var registeredGenes =
            await _genomicsService.GetRegisteredGeneNamesAsync();

        var registeredGeneSet = new HashSet<string>(
            registeredGenes,
            StringComparer.OrdinalIgnoreCase);

        foreach (var geneName in model.GeneValues.Keys)
        {
            if (!registeredGeneSet.Contains(geneName))
            {
                ModelState.AddModelError(
                    nameof(model.GeneValues),
                    $"'{geneName}' is not a registered active gene.");
            }
        }
    }

    private static void NormalizeModel(
        GenomicsRecordViewModel model)
    {
        if (model.GeneValues == null)
        {
            model.GeneValues =
                new Dictionary<string, bool?>(
                    StringComparer.OrdinalIgnoreCase);
        }

        var stringProperties = model.GetType()
            .GetProperties()
            .Where(property =>
                property.CanRead &&
                property.CanWrite &&
                property.PropertyType == typeof(string));

        foreach (var property in stringProperties)
        {
            var value = property.GetValue(model) as string;

            if (value != null)
                property.SetValue(model, value.Trim());
        }

        model.GeneValues = new Dictionary<string, bool?>(
            model.GeneValues,
            StringComparer.OrdinalIgnoreCase);
    }
}
