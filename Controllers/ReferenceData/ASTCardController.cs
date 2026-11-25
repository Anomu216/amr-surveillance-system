using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers.ReferenceData;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.GenomicsStaff)]
public class ASTCardController : Controller
{
    private readonly ApplicationDbContext _context;

    public ASTCardController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cards = await _context.ASTCards
            .AsNoTracking()
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .OrderBy(c => c.ASTCard)
            .ToListAsync();

        var model = cards
            .Select(c => new ASTCardListItemViewModel
            {
                ASTCard = c.ASTCard,
                Description = c.Description,
                IsActive = c.IsActive,
                AntibioticCount = c.ASTCardAntibiotics.Count,
                MappedOrganisms = c.ASTCardOrganisms
                    .Select(o => o.WHONETID)
                    .OrderBy(w => w)
                    .ToList()
            })
            .ToList();

        return View(
            "~/Views/ReferenceData/ASTCard/Index.cshtml",
            model);
    }

    // =========================================================
    // CREATE - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ASTCardEditViewModel
        {
            IsActive = true
        };

        await PopulateLookupsAsync(model);

        return View(
            "~/Views/ReferenceData/ASTCard/Edit.cshtml",
            model);
    }

    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        ASTCardEditViewModel model,
        string? newAntibioticCode)
    {
        var normalizedCode = model.ASTCard?.Trim().ToUpperInvariant() ?? string.Empty;
        model.ASTCard = normalizedCode;

        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            ModelState.AddModelError(
                nameof(model.ASTCard),
                "AST Card code is required.");
        }
        else if (await _context.ASTCards.AnyAsync(c => c.ASTCard == normalizedCode))
        {
            ModelState.AddModelError(
                nameof(model.ASTCard),
                $"AST Card '{normalizedCode}' already exists.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(
                "~/Views/ReferenceData/ASTCard/Edit.cshtml",
                model);
        }

        var card = new ASTCardDefinition
        {
            ASTCard = normalizedCode,
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            IsActive = model.IsActive
        };

        var distinctOrgs = (model.SelectedWHONETIDs ?? new List<string>())
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var whonetId in distinctOrgs)
        {
            card.ASTCardOrganisms.Add(new ASTCardOrganism
            {
                ASTCard = normalizedCode,
                WHONETID = whonetId
            });
        }

        if (!string.IsNullOrWhiteSpace(newAntibioticCode))
        {
            var abxCode = newAntibioticCode.Trim().ToUpperInvariant();
            var abx = await _context.Antibiotics
                .FirstOrDefaultAsync(a => a.AntibioticCode == abxCode);

            if (abx != null)
            {
                card.ASTCardAntibiotics.Add(new ASTCardAntibiotic
                {
                    ASTCard = normalizedCode,
                    AntibioticCode = abx.AntibioticCode,
                    DisplayOrder = 1
                });

                AppendCardToAntibioticLegacyField(abx, normalizedCode);
            }
        }

        _context.ASTCards.Add(card);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"AST Card '{normalizedCode}' created successfully.";

        return RedirectToAction(nameof(Edit), new { id = normalizedCode });
    }

    // =========================================================
    // EDIT - GET
    // =========================================================

    [HttpGet("ASTCard/Edit/{**id}")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id).Trim().ToUpperInvariant();

        var card = await _context.ASTCards
            .AsNoTracking()
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .FirstOrDefaultAsync(c => c.ASTCard == id);

        if (card == null)
            return NotFound();

        var antibioticsByCode = await _context.Antibiotics
            .AsNoTracking()
            .ToDictionaryAsync(
                a => a.AntibioticCode,
                StringComparer.OrdinalIgnoreCase);

        var model = new ASTCardEditViewModel
        {
            ASTCard = card.ASTCard,
            Description = card.Description,
            IsActive = card.IsActive,
            SelectedWHONETIDs = card.ASTCardOrganisms
                .Select(o => o.WHONETID)
                .ToList(),
            Antibiotics = card.ASTCardAntibiotics
                .OrderBy(ca => ca.DisplayOrder)
                .ThenBy(ca => ca.AntibioticCode)
                .Select(ca =>
                {
                    antibioticsByCode.TryGetValue(ca.AntibioticCode, out var abx);
                    return new ASTCardPanelItemViewModel
                    {
                        AntibioticCode = ca.AntibioticCode,
                        AntibioticName = abx?.AntibioticName ?? ca.AntibioticCode,
                        AntibioticClassName = abx?.AntibioticClassName ?? string.Empty,
                        DisplayOrder = ca.DisplayOrder
                    };
                })
                .ToList()
        };

        await PopulateLookupsAsync(model);

        return View(
            "~/Views/ReferenceData/ASTCard/Edit.cshtml",
            model);
    }

    // =========================================================
    // EDIT - POST
    // =========================================================

    [HttpPost("ASTCard/Edit/{**id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        ASTCardEditViewModel model,
        string? newAntibioticCode)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id).Trim().ToUpperInvariant();
        model.ASTCard = id;

        if (!ModelState.IsValid)
        {
            await PopulateLookupsAsync(model);
            return View(
                "~/Views/ReferenceData/ASTCard/Edit.cshtml",
                model);
        }

        var card = await _context.ASTCards
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .FirstOrDefaultAsync(c => c.ASTCard == id);

        if (card == null)
            return NotFound();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        card.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();
        card.IsActive = model.IsActive;

        // -----------------------------------------------------
        // 1. Synchronize Organism Mappings
        // -----------------------------------------------------
        var targetOrgs = (model.SelectedWHONETIDs ?? new List<string>())
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orgsToRemove = card.ASTCardOrganisms
            .Where(o => !targetOrgs.Contains(o.WHONETID))
            .ToList();

        foreach (var rem in orgsToRemove)
        {
            card.ASTCardOrganisms.Remove(rem);
        }

        var existingOrgs = card.ASTCardOrganisms
            .Select(o => o.WHONETID)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var whonetId in targetOrgs)
        {
            if (!existingOrgs.Contains(whonetId))
            {
                card.ASTCardOrganisms.Add(new ASTCardOrganism
                {
                    ASTCard = id,
                    WHONETID = whonetId
                });
            }
        }

        // -----------------------------------------------------
        // 2. Synchronize Panel Antibiotics & DisplayOrder
        // -----------------------------------------------------
        var submittedItems = model.Antibiotics ?? new List<ASTCardPanelItemViewModel>();
        var codesToRemove = submittedItems
            .Where(item => item.Remove)
            .Select(item => item.AntibioticCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orderByCode = submittedItems
            .Where(item => !item.Remove)
            .ToDictionary(
                item => item.AntibioticCode,
                item => item.DisplayOrder,
                StringComparer.OrdinalIgnoreCase);

        var existingPanelByCode = card.ASTCardAntibiotics
            .ToDictionary(
                ca => ca.AntibioticCode,
                StringComparer.OrdinalIgnoreCase);

        // Remove unchecked antibiotics from card & legacy Antibiotic.ASTCard
        foreach (var code in codesToRemove)
        {
            if (existingPanelByCode.TryGetValue(code, out var panelRow))
            {
                card.ASTCardAntibiotics.Remove(panelRow);
            }

            var abx = await _context.Antibiotics
                .FirstOrDefaultAsync(a => a.AntibioticCode == code);
            if (abx != null)
            {
                RemoveCardFromAntibioticLegacyField(abx, id);
            }
        }

        // Update DisplayOrder on retained items and normalize sequence (1..N)
        var retained = card.ASTCardAntibiotics
            .OrderBy(ca => orderByCode.TryGetValue(ca.AntibioticCode, out var ord)
                ? ord
                : ca.DisplayOrder)
            .ThenBy(ca => ca.AntibioticCode)
            .ToList();

        for (var i = 0; i < retained.Count; i++)
        {
            retained[i].DisplayOrder = i + 1;
        }

        // Add newly selected antibiotic if requested
        if (!string.IsNullOrWhiteSpace(newAntibioticCode))
        {
            var addCode = newAntibioticCode.Trim().ToUpperInvariant();
            if (!card.ASTCardAntibiotics.Any(ca =>
                    string.Equals(ca.AntibioticCode, addCode, StringComparison.OrdinalIgnoreCase)))
            {
                var abx = await _context.Antibiotics
                    .FirstOrDefaultAsync(a => a.AntibioticCode == addCode);

                if (abx != null)
                {
                    card.ASTCardAntibiotics.Add(new ASTCardAntibiotic
                    {
                        ASTCard = id,
                        AntibioticCode = abx.AntibioticCode,
                        DisplayOrder = retained.Count + 1
                    });

                    AppendCardToAntibioticLegacyField(abx, id);
                }
            }
        }

        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"AST Card '{id}' updated successfully.";

        return RedirectToAction(nameof(Edit), new { id });
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

        var normalizedCode = id.Trim().ToUpperInvariant();

        var card = await _context.ASTCards
            .Include(c => c.ASTCardAntibiotics)
            .FirstOrDefaultAsync(c => c.ASTCard == normalizedCode);

        if (card == null)
            return NotFound();

        var isUsedInWorksheets = await _context.Worksheets
            .AsNoTracking()
            .AnyAsync(w => w.ASTCard != null && w.ASTCard.Contains(normalizedCode));

        if (isUsedInWorksheets)
        {
            TempData["Error"] =
                $"AST Card '{normalizedCode}' cannot be deleted because it is referenced by existing Worksheet records. Mark it as Inactive instead.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        foreach (var panelItem in card.ASTCardAntibiotics)
        {
            var abx = await _context.Antibiotics
                .FirstOrDefaultAsync(a => a.AntibioticCode == panelItem.AntibioticCode);
            if (abx != null)
            {
                RemoveCardFromAntibioticLegacyField(abx, normalizedCode);
            }
        }

        _context.ASTCards.Remove(card);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        TempData["Success"] = $"AST Card '{normalizedCode}' deleted successfully.";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private async Task PopulateLookupsAsync(ASTCardEditViewModel model)
    {
        var organisms = await _context.Organisms
            .AsNoTracking()
            .OrderBy(o => o.WHONETID)
            .Select(o => o.WHONETID)
            .ToListAsync();

        var selectedOrgSet = new HashSet<string>(
            model.SelectedWHONETIDs ?? new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        model.AvailableOrganisms = organisms
            .Select(w => new SelectListItem
            {
                Value = w,
                Text = w,
                Selected = selectedOrgSet.Contains(w)
            })
            .ToList();

        var alreadyAssignedCodes = model.Antibiotics
            .Select(a => a.AntibioticCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activeAntibiotics = await _context.Antibiotics
            .AsNoTracking()
            .Where(a => a.Active)
            .OrderBy(a => a.AntibioticName)
            .ToListAsync();

        model.AvailableAntibioticsToAdd = activeAntibiotics
            .Where(a => !alreadyAssignedCodes.Contains(a.AntibioticCode))
            .Select(a => new SelectListItem
            {
                Value = a.AntibioticCode,
                Text = $"{a.AntibioticCode} — {a.AntibioticName}"
            })
            .ToList();
    }

    private static void AppendCardToAntibioticLegacyField(
        Antibiotic antibiotic,
        string cardCode)
    {
        var cards = (antibiotic.ASTCard ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!cards.Contains(cardCode, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(cardCode);
            antibiotic.ASTCard = string.Join(";", cards);
        }
    }

    private static void RemoveCardFromAntibioticLegacyField(
        Antibiotic antibiotic,
        string cardCode)
    {
        var cards = (antibiotic.ASTCard ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Where(c => !string.Equals(c, cardCode, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        antibiotic.ASTCard = string.Join(";", cards);
    }
}