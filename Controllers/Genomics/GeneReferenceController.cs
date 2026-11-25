
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using AMRSurveillance.Imports.Templates;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AMRSurveillance.Security;
using Microsoft.AspNetCore.Authorization;

namespace AMRSurveillance.Controllers.Genomics;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.GenomicsStaff)]
public class GeneReferenceController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IGeneReferenceSchemaService _schemaService;
    private readonly IImportWorkflow<GeneReference> _importWorkflow;

    private const string ViewFolder =
        "~/Views/Genomics/GeneReference/";

    private const string GeneReferenceImportSessionKey =
        "GeneReferenceImportRecords";

    public GeneReferenceController(
        ApplicationDbContext context,
        IGeneReferenceSchemaService schemaService,
        IImportWorkflow<GeneReference> importWorkflow)
    {
        _context = context;
        _schemaService = schemaService;
        _importWorkflow = importWorkflow;
    }

    // ============================================================
    // INDEX
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _context.GeneReferences
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(g =>
                g.GeneName.Contains(search) ||
                (g.GeneDescription != null &&
                 g.GeneDescription.Contains(search)) ||
                (g.GeneCategory != null &&
                 g.GeneCategory.Contains(search)) ||
                (g.ApplicableOrganism != null &&
                 g.ApplicableOrganism.Contains(search)));
        }

        var genes = await query
            .OrderBy(g => g.GeneName)
            .Select(g => new GeneReferenceViewModel
            {
                GeneName = g.GeneName,
                GeneDescription = g.GeneDescription,
                GeneCategory = g.GeneCategory,
                ApplicableOrganism = g.ApplicableOrganism,
                IsActive = g.IsActive
            })
            .ToListAsync();

        ViewBag.Search = search;

        return View($"{ViewFolder}Index.cshtml", genes);
    }

    // ============================================================
    // DETAILS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Details(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var gene = await _context.GeneReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GeneName == id);

        if (gene == null)
            return NotFound();

        var model = new GeneReferenceViewModel
        {
            GeneName = gene.GeneName,
            GeneDescription = gene.GeneDescription,
            GeneCategory = gene.GeneCategory,
            ApplicableOrganism = gene.ApplicableOrganism,
            IsActive = gene.IsActive
        };

        return View($"{ViewFolder}Details.cshtml", model);
    }

    // ============================================================
    // CREATE - GET
    // ============================================================

    [HttpGet]
    public IActionResult Create()
    {
        var model = new GeneReferenceViewModel
        {
            IsActive = true
        };

        return View($"{ViewFolder}Create.cshtml", model);
    }

    // ============================================================
    // CREATE - POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        GeneReferenceViewModel model)
    {
        if (!ModelState.IsValid)
            return View($"{ViewFolder}Create.cshtml", model);

        var geneName = model.GeneName.Trim();

        var alreadyExists = await _context.GeneReferences
            .AnyAsync(g => g.GeneName == geneName);

        if (alreadyExists)
        {
            ModelState.AddModelError(
                nameof(model.GeneName),
                "This gene already exists in Gene Reference.");

            return View($"{ViewFolder}Create.cshtml", model);
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            await _schemaService.EnsureGeneColumnAsync(geneName);

            var gene = new GeneReference
            {
                GeneName = geneName,
                GeneDescription = model.GeneDescription?.Trim(),
                GeneCategory = model.GeneCategory?.Trim(),
                ApplicableOrganism = model.ApplicableOrganism?.Trim(),
                IsActive = model.IsActive
            };

            _context.GeneReferences.Add(gene);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                $"Gene '{geneName}' was registered successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await transaction.RollbackAsync();

            ModelState.AddModelError(
                string.Empty,
                "The gene could not be registered. Check the database configuration and try again.");

            return View($"{ViewFolder}Create.cshtml", model);
        }
    }

    // ============================================================
    // EDIT - GET
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var gene = await _context.GeneReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GeneName == id);

        if (gene == null)
            return NotFound();

        var model = new GeneReferenceViewModel
        {
            GeneName = gene.GeneName,
            GeneDescription = gene.GeneDescription,
            GeneCategory = gene.GeneCategory,
            ApplicableOrganism = gene.ApplicableOrganism,
            IsActive = gene.IsActive
        };

        return View($"{ViewFolder}Edit.cshtml", model);
    }

    // ============================================================
    // EDIT - POST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        GeneReferenceViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        if (!string.Equals(
                id,
                model.GeneName?.Trim(),
                StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                nameof(model.GeneName),
                "Changing a gene name is not supported. Register a separate gene reference instead.");
        }

        if (!ModelState.IsValid)
            return View($"{ViewFolder}Edit.cshtml", model);

        var gene = await _context.GeneReferences
            .FirstOrDefaultAsync(g => g.GeneName == id);

        if (gene == null)
            return NotFound();

        gene.GeneDescription = model.GeneDescription?.Trim();
        gene.GeneCategory = model.GeneCategory?.Trim();
        gene.ApplicableOrganism = model.ApplicableOrganism?.Trim();
        gene.IsActive = model.IsActive;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] =
            $"Gene '{gene.GeneName}' was updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // DELETE - GET CONFIRMATION
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var gene = await _context.GeneReferences
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GeneName == id);

        if (gene == null)
            return NotFound();

        ViewBag.HasEnteredValues =
            await HasEnteredGeneValuesAsync(gene.GeneName);

        return View($"{ViewFolder}Delete.cshtml", gene);
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

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var gene = await _context.GeneReferences
                .FirstOrDefaultAsync(g => g.GeneName == id);

            if (gene == null)
                return NotFound();

            var hasEnteredValues =
                await HasEnteredGeneValuesAsync(gene.GeneName);

            if (hasEnteredValues)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    $"Gene '{gene.GeneName}' cannot be deleted because its Genomics column contains recorded values.";

                return RedirectToAction(nameof(Index));
            }

            var columnExists = await _context.Database
                .SqlQuery<int>($"""
                    SELECT COUNT(*) AS [Value]
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'dbo.Genomics')
                      AND name = {gene.GeneName}
                    """)
                .SingleAsync();

            if (columnExists > 0)
            {
                var safeColumnName =
                    "[" + gene.GeneName.Replace("]", "]]") + "]";

                await _context.Database.ExecuteSqlAsync(
                    $"ALTER TABLE dbo.Genomics DROP COLUMN {safeColumnName}");
            }

            _context.GeneReferences.Remove(gene);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                $"Gene '{gene.GeneName}' was deleted successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch
        {
            await transaction.RollbackAsync();

            TempData["ErrorMessage"] =
                "The gene could not be deleted. Check database dependencies and try again.";

            return RedirectToAction(nameof(Index));
        }
    }

    // ============================================================
    // CHECK WHETHER A GENE COLUMN CONTAINS RECORDED VALUES
    // ============================================================

    private async Task<bool> HasEnteredGeneValuesAsync(
        string geneName)
    {
        var columnExists = await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value]
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.Genomics')
                  AND name = {geneName}
                """)
            .SingleAsync();

        if (columnExists == 0)
            return false;

        var safeColumnName =
            "[" + geneName.Replace("]", "]]") + "]";

        var result = await _context.Database
            .SqlQuery<int>($"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM dbo.Genomics
                        WHERE {safeColumnName} IS NOT NULL
                    )
                    THEN 1
                    ELSE 0
                END AS [Value]
                """)
            .SingleAsync();

        return result == 1;
    }

    // ============================================================
    // IMPORT - GET
    // ============================================================

    [HttpGet]
    public IActionResult Import()
    {
        var model = new ImportViewModel<GeneReference>();

        return View($"{ViewFolder}Import.cshtml", model);
    }

    // ============================================================
    // IMPORT - VALIDATE AND PREVIEW
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(
        ImportViewModel<GeneReference> model)
    {
        var requiredHeaders = GeneReferenceImportDefinition.Fields
            .Where(field => field.Required)
            .Select(field => field.Name)
            .ToList();

        if (model.File == null || model.File.Length == 0)
        {
            model.IsValidated = true;

            model.Errors.Add(new ImportRowError
            {
                RowNumber = 0,
                ColumnName = string.Empty,
                Message = "Please select an Excel file."
            });

            return View($"{ViewFolder}Import.cshtml", model);
        }

        var validatedModel = await _importWorkflow.ValidateAsync(
            model.File,
            requiredHeaders,
            GeneReferenceImportSessionKey);

        return View($"{ViewFolder}Import.cshtml", validatedModel);
    }

    // ============================================================
    // IMPORT - CONFIRM
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmImport()
    {
        var summary = await _importWorkflow.ConfirmImportAsync(
            GeneReferenceImportSessionKey);

        TempData["ImportSuccess"] =
            $"{summary.SuccessfulRows} gene reference record(s) imported successfully.";

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // IMPORT - DOWNLOAD TEMPLATE
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> DownloadImportTemplate()
    {
        return await _importWorkflow.DownloadTemplateAsync(
            GeneReferenceImportDefinition.Fields,
            "GeneReferenceImportTemplate.xlsx");
    }

    // ============================================================
    // IMPORT - CLEAR SESSION
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearImport()
    {
        _importWorkflow.ClearSession(
            GeneReferenceImportSessionKey);

        return RedirectToAction(nameof(Import));
    }
}
