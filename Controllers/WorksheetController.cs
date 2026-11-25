using System.Text.Json;
using System.Text.RegularExpressions;
using AMRSurveillance.Data;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers;

[Authorize(Roles = AppRoles.SystemAdministrator + "," + AppRoles.Analyst + "," + AppRoles.DataEntryStaff)]
public class WorksheetController(
    IWorksheetService service,
    ILookupService lookupService,
    IASTConfigurationService astConfigurationService,
    IASTSchemaService astSchemaService,
    IWorksheetImportService worksheetImportService,
    ApplicationDbContext context) : Controller
{
    private const string ViewPath = "~/Views/Worksheet/";
    private const string StaphylococcusAureus = "Staphylococcus aureus";

    private static readonly Regex IsoSuffixRegex =
        new(@"^[A-Z]$", RegexOptions.Compiled);

    private static readonly Regex AmrSuffixRegex =
        new(@"^[A-Z0-9]{2,}$", RegexOptions.Compiled);

    // =====================================================
    // WORKSHEET INDEX
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Index(string? search)
    {
        var worksheets = await service.GetAllAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            worksheets = worksheets
                .Where(w =>
                    w.PatientID.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||
                    w.IsolateID.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||
                    (w.ManuscriptID != null &&
                     w.ManuscriptID.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)) ||
                    (w.WHONETID != null &&
                     w.WHONETID.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)) ||
                    (w.ASTCard != null &&
                     w.ASTCard.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        ViewBag.Search = search;

        var model = worksheets.Select(w => new WorksheetViewModel
        {
            PatientID = w.PatientID,
            IsolateID = w.IsolateID,
            CultureResult = w.CultureResult,
            ManuscriptID = w.ManuscriptID,
            AcceptedID = w.AcceptedID,
            WHONETID = w.WHONETID,
            GramReaction = w.GramReaction,
            Morphology = w.Morphology,
            Hemolysis = w.Hemolysis,
            Catalase = w.Catalase,
            Oxidase = w.Oxidase,
            Coagulase = w.Coagulase,
            LactoseFermentation = w.LactoseFermentation,
            Comments = w.Comments,
            ASTCards = ParseASTCards(w.ASTCard),
            MDR = w.MDR,
            Carbapenemase = w.Carbapenemase,
            MRSA = w.MRSA,
            ESBL = w.ESBL,
            CefoxitinScreen = w.CefoxitinScreen,
            InducibleClindamycinResistance =
                w.InducibleClindamycinResistance,
            ASTComments = w.ASTComments,
            Antibiotics = []
        }).ToList();

        return View($"{ViewPath}Index.cshtml", model);
    }

    // =====================================================
    // DOWNLOAD IMPORT TEMPLATE
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Template()
    {
        var file = await worksheetImportService.GenerateTemplateAsync();

        return File(
            file,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Worksheet_Import_Template.xlsx");
    }

    // =====================================================
    // IMPORT
    // =====================================================

    [HttpGet]
    public IActionResult Import()
    {
        return View($"{ViewPath}Import.cshtml");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError(
                "file",
                "Please select an Excel file to import.");

            return View($"{ViewPath}Import.cshtml");
        }

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                "file",
                "Only .xlsx Excel files are supported.");

            return View($"{ViewPath}Import.cshtml");
        }

        WorksheetImportResult result;

        await using (var stream = file.OpenReadStream())
        {
            result = await worksheetImportService.ImportAsync(stream);
        }

        ViewBag.FileName = file.FileName;

        return View($"{ViewPath}Import.cshtml", result);
    }

    // =====================================================
    // CREATE - GET
    // =====================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new WorksheetViewModel();
        var cardCatalog = await LoadCardCatalogAsync();

        await PopulateLookups(model);
        await PopulateASTConfiguration(model, cardCatalog);

        return View($"{ViewPath}Create.cshtml", model);
    }

    // =====================================================
    // CREATE - POST
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorksheetViewModel model)
    {
        NormalizeWorksheetModel(model);

        var cardCatalog = await LoadCardCatalogAsync();

        await PopulateLookups(model);
        await PopulateASTConfiguration(model, cardCatalog);

        var patientID = model.PatientID.Trim();
        var isolateID = model.IsolateID.Trim();

        if (!string.IsNullOrWhiteSpace(patientID) &&
            !await service.PatientExistsAsync(patientID))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                "The specified Patient ID does not exist in dbo.Patients. Register the patient first.");
        }

        if (!string.IsNullOrWhiteSpace(isolateID) &&
            await service.ExistsAsync(isolateID))
        {
            ModelState.AddModelError(
                nameof(model.IsolateID),
                "An isolate with this Isolate ID already exists.");
        }

        await ValidateWorksheetBusinessRulesAsync(model, cardCatalog);

        if (!ModelState.IsValid)
            return View($"{ViewPath}Create.cshtml", model);

        bool isNoGrowth = IsNoGrowth(model.CultureResult);

        if (isNoGrowth)
        {
            ClearGrowthSpecificFields(model);
        }

        var derived = isNoGrowth
            ? new DerivedWorksheetClassification(null, null, null)
            : await CalculateDerivedForModelAsync(model);

        var normalizedCards = isNoGrowth
            ? []
            : NormalizeASTCards(model.ASTCards);

        var worksheet = new Worksheet
        {
            IsolateID = isolateID,
            PatientID = patientID,
            CultureResult = model.CultureResult.Trim(),
            ManuscriptID = Clean(model.ManuscriptID),
            AcceptedID = isNoGrowth ? null : Clean(model.AcceptedID),
            WHONETID = isNoGrowth ? null : Clean(model.WHONETID),
            GramReaction = isNoGrowth ? null : Clean(model.GramReaction),
            Morphology = isNoGrowth ? null : Clean(model.Morphology),
            Hemolysis = isNoGrowth ? null : Clean(model.Hemolysis),
            Catalase = isNoGrowth ? null : Clean(model.Catalase),
            Oxidase = isNoGrowth ? null : Clean(model.Oxidase),
            Coagulase = isNoGrowth ? null : Clean(model.Coagulase),
            LactoseFermentation = isNoGrowth ? null : Clean(model.LactoseFermentation),
            Comments = Clean(model.Comments),

            ASTCard = normalizedCards.Count > 0
                ? string.Join(";", normalizedCards)
                : null,

            ESBL = isNoGrowth ? null : Clean(model.ESBL),
            CefoxitinScreen = isNoGrowth ? null : Clean(model.CefoxitinScreen),
            InducibleClindamycinResistance =
                isNoGrowth ? null : Clean(model.InducibleClindamycinResistance),
            ASTComments = isNoGrowth ? null : Clean(model.ASTComments),

            MDR = derived.MDR,
            MRSA = derived.MRSA,
            Carbapenemase = derived.Carbapenemase
        };

        await service.CreateAsync(worksheet);

        await astSchemaService.SaveASTResultsAsync(
            isolateID,
            isNoGrowth ? [] : model.Antibiotics);

        if (!isNoGrowth)
        {
            SetCefoxitinOxacillinWarning(model);
        }

        TempData["Success"] = "Worksheet record created successfully.";

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // EDIT - GET
    // =====================================================

    [HttpGet("Worksheet/Edit/{**id}")]
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id).Trim();

        var worksheet = await service.GetByIdAsync(id);

        if (worksheet == null)
            return NotFound();

        var model = new WorksheetViewModel
        {
            PatientID = worksheet.PatientID,
            IsolateID = worksheet.IsolateID,
            CultureResult = worksheet.CultureResult,
            ManuscriptID = worksheet.ManuscriptID,
            AcceptedID = worksheet.AcceptedID,
            WHONETID = worksheet.WHONETID,
            GramReaction = worksheet.GramReaction,
            Morphology = worksheet.Morphology,
            Hemolysis = worksheet.Hemolysis,
            Catalase = worksheet.Catalase,
            Oxidase = worksheet.Oxidase,
            Coagulase = worksheet.Coagulase,
            LactoseFermentation = worksheet.LactoseFermentation,
            Comments = worksheet.Comments,
            ASTCards = ParseASTCards(worksheet.ASTCard),
            ESBL = worksheet.ESBL,
            MDR = worksheet.MDR,
            Carbapenemase = worksheet.Carbapenemase,
            MRSA = worksheet.MRSA,
            CefoxitinScreen = worksheet.CefoxitinScreen,
            InducibleClindamycinResistance =
                worksheet.InducibleClindamycinResistance,
            ASTComments = worksheet.ASTComments
        };

        var cardCatalog = await LoadCardCatalogAsync();

        await PopulateLookups(model);
        await PopulateASTConfiguration(model, cardCatalog);

        await astSchemaService.LoadASTResultsAsync(
            id,
            model.Antibiotics);

        ApplyDatabaseCardOrdering(model, cardCatalog);

        return View($"{ViewPath}Edit.cshtml", model);
    }

    // =====================================================
    // EDIT - POST
    // =====================================================

    [HttpPost("Worksheet/Edit/{**id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        WorksheetViewModel model)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id).Trim();

        NormalizeWorksheetModel(model);

        var cardCatalog = await LoadCardCatalogAsync();

        await PopulateLookups(model);
        await PopulateASTConfiguration(model, cardCatalog);

        var existing = await service.GetByIdAsync(id);

        if (existing == null)
            return NotFound();

        if (!string.Equals(
                id,
                model.IsolateID.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                nameof(model.IsolateID),
                "The Isolate ID cannot be changed because it is the primary key.");
        }

        var patientID = model.PatientID.Trim();

        if (!string.IsNullOrWhiteSpace(patientID) &&
            !await service.PatientExistsAsync(patientID))
        {
            ModelState.AddModelError(
                nameof(model.PatientID),
                "The specified Patient ID does not exist in dbo.Patients.");
        }

        await ValidateWorksheetBusinessRulesAsync(model, cardCatalog);

        if (!ModelState.IsValid)
            return View($"{ViewPath}Edit.cshtml", model);

        bool isNoGrowth = IsNoGrowth(model.CultureResult);

        if (isNoGrowth)
        {
            ClearGrowthSpecificFields(model);
        }

        var derived = isNoGrowth
            ? new DerivedWorksheetClassification(null, null, null)
            : await CalculateDerivedForModelAsync(model);

        var normalizedCards = isNoGrowth
            ? []
            : NormalizeASTCards(model.ASTCards);

        existing.PatientID = patientID;
        existing.CultureResult = model.CultureResult.Trim();
        existing.ManuscriptID = Clean(model.ManuscriptID);
        existing.AcceptedID = isNoGrowth ? null : Clean(model.AcceptedID);
        existing.WHONETID = isNoGrowth ? null : Clean(model.WHONETID);
        existing.GramReaction = isNoGrowth ? null : Clean(model.GramReaction);
        existing.Morphology = isNoGrowth ? null : Clean(model.Morphology);
        existing.Hemolysis = isNoGrowth ? null : Clean(model.Hemolysis);
        existing.Catalase = isNoGrowth ? null : Clean(model.Catalase);
        existing.Oxidase = isNoGrowth ? null : Clean(model.Oxidase);
        existing.Coagulase = isNoGrowth ? null : Clean(model.Coagulase);
        existing.LactoseFermentation = isNoGrowth ? null : Clean(model.LactoseFermentation);
        existing.Comments = Clean(model.Comments);

        existing.ASTCard = normalizedCards.Count > 0
            ? string.Join(";", normalizedCards)
            : null;

        existing.ESBL = isNoGrowth ? null : Clean(model.ESBL);
        existing.CefoxitinScreen = isNoGrowth ? null : Clean(model.CefoxitinScreen);
        existing.InducibleClindamycinResistance =
            isNoGrowth ? null : Clean(model.InducibleClindamycinResistance);
        existing.ASTComments = isNoGrowth ? null : Clean(model.ASTComments);

        existing.MDR = derived.MDR;
        existing.MRSA = derived.MRSA;
        existing.Carbapenemase = derived.Carbapenemase;

        await service.UpdateAsync(id, existing);

        await astSchemaService.SaveASTResultsAsync(
            id,
            isNoGrowth ? [] : model.Antibiotics);

        if (!isNoGrowth)
        {
            SetCefoxitinOxacillinWarning(model);
        }

        TempData["Success"] = "Worksheet record updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // DELETE
    // =====================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        id = Uri.UnescapeDataString(id).Trim();

        var worksheet = await service.GetByIdAsync(id);

        if (worksheet == null)
        {
            TempData["Error"] = "Worksheet record was not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await service.DeleteAsync(id);
            TempData["Success"] = "Worksheet record deleted successfully.";
        }
        catch
        {
            TempData["Error"] =
                "This worksheet record cannot be deleted because it is being used by another record.";
        }

        return RedirectToAction(nameof(Index));
    }

    // =====================================================
    // DATABASE-DRIVEN CARD CATALOG LOADER
    // =====================================================

    private sealed class CardCatalog
    {
        public Dictionary<string, ASTCardDefinition> CardsByCode { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<string>> OrderedAntibioticsByCard { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, int>> DisplayOrdersByCard { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> AntibioticSetByCard { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, HashSet<string>> AllowedOrganismsByCard { get; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<CardCatalog> LoadCardCatalogAsync()
    {
        var catalog = new CardCatalog();

        var cards = await context.ASTCards
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.ASTCardOrganisms)
            .Include(c => c.ASTCardAntibiotics)
            .ToListAsync();

        foreach (var card in cards)
        {
            var code = card.ASTCard.Trim().ToUpperInvariant();
            catalog.CardsByCode[code] = card;

            var sortedEntries = card.ASTCardAntibiotics
                .OrderBy(a => a.DisplayOrder)
                .ThenBy(a => a.AntibioticCode, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orderedCodes = new List<string>();
            var displayOrderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int idx = 0; idx < sortedEntries.Count; idx++)
            {
                var abxCode = sortedEntries[idx].AntibioticCode.Trim().ToUpperInvariant();
                if (!displayOrderMap.ContainsKey(abxCode))
                {
                    orderedCodes.Add(abxCode);
                    displayOrderMap[abxCode] = sortedEntries[idx].DisplayOrder > 0
                        ? sortedEntries[idx].DisplayOrder
                        : idx + 1;
                }
            }

            catalog.OrderedAntibioticsByCard[code] = orderedCodes;
            catalog.DisplayOrdersByCard[code] = displayOrderMap;
            catalog.AntibioticSetByCard[code] = new HashSet<string>(
                orderedCodes,
                StringComparer.OrdinalIgnoreCase);

            var allowedOrgs = card.ASTCardOrganisms
                .Select(o => o.WHONETID.Trim())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            catalog.AllowedOrganismsByCard[code] = allowedOrgs;
        }

        return catalog;
    }

    // =====================================================
    // VALIDATE WORKSHEET BUSINESS RULES
    // =====================================================

    private async Task ValidateWorksheetBusinessRulesAsync(
        WorksheetViewModel model,
        CardCatalog cardCatalog)
    {
        var patientID = model.PatientID?.Trim() ?? string.Empty;
        var isolateID = model.IsolateID?.Trim() ?? string.Empty;
        var cultureResult = model.CultureResult?.Trim() ?? string.Empty;

        bool isNoGrowth = IsNoGrowth(cultureResult);
        bool isGrowth = IsGrowth(cultureResult);

        if (!isNoGrowth && !isGrowth)
        {
            ModelState.AddModelError(
                nameof(model.CultureResult),
                "Culture Result must be either Growth or No Growth.");
            return;
        }

        if (isNoGrowth)
        {
            if (!string.Equals(
                    isolateID,
                    patientID,
                    StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.IsolateID),
                    "When Culture Result is No Growth, Isolate ID must match the Patient ID exactly.");
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(patientID))
        {
            var expectedPrefix = $"{patientID}-";

            if (!isolateID.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var exampleSuffix = patientID.StartsWith("ISO", StringComparison.OrdinalIgnoreCase)
                    ? $"{patientID}-A"
                    : $"{patientID}-01";

                ModelState.AddModelError(
                    nameof(model.IsolateID),
                    $"For Growth cultures, Isolate ID must start with '{expectedPrefix}' (for example, {exampleSuffix}).");
            }
            else
            {
                var suffix = isolateID[expectedPrefix.Length..];

                if (patientID.StartsWith("ISO", StringComparison.OrdinalIgnoreCase))
                {
                    if (!IsoSuffixRegex.IsMatch(suffix))
                    {
                        ModelState.AddModelError(
                            nameof(model.IsolateID),
                            $"When Patient ID starts with ISO, Isolate ID must be '{patientID}-' followed by a single letter (e.g., {patientID}-A).");
                    }
                }
                else if (patientID.StartsWith("AMR", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AmrSuffixRegex.IsMatch(suffix))
                    {
                        ModelState.AddModelError(
                            nameof(model.IsolateID),
                            $"When Patient ID starts with AMR, Isolate ID must be '{patientID}-' followed by two or more alphanumeric characters (e.g., {patientID}-01 or {patientID}-A1).");
                    }
                }
            }
        }

        var whonetID = Clean(model.WHONETID);

        if (whonetID != null)
        {
            var organismExists = await context.Organisms
                .AsNoTracking()
                .AnyAsync(o => o.WHONETID == whonetID);

            if (!organismExists)
            {
                ModelState.AddModelError(
                    nameof(model.WHONETID),
                    "The selected WHONET ID does not exist.");
            }
        }

        ValidatePhenotypicCharacteristics(model);

        var selectedCards = NormalizeASTCards(model.ASTCards);
        ValidateOrganismAndCardCompatibility(model, selectedCards, cardCatalog);
        ValidateAntibioticsAgainstCardsAndPairs(model, selectedCards, cardCatalog);
    }

    private void ValidatePhenotypicCharacteristics(WorksheetViewModel model)
    {
        var gram = Clean(model.GramReaction);
        var morphology = Clean(model.Morphology);
        var lactose = Clean(model.LactoseFermentation);
        var oxidase = Clean(model.Oxidase);
        var catalase = Clean(model.Catalase);
        var coagulase = Clean(model.Coagulase);
        var orgName = model.WHONETID?.Trim() ?? string.Empty;

        bool isYeastOrg =
            orgName.StartsWith("Candida", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Cryptococcus", StringComparison.OrdinalIgnoreCase);

        bool isKnownGPOrg =
            orgName.StartsWith("Staphylococcus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Streptococcus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Enterococcus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Listeria", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Bacillus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Corynebacterium", StringComparison.OrdinalIgnoreCase);

        bool isKnownGNOrg =
            orgName.StartsWith("Escherichia", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Klebsiella", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Enterobacter", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Pseudomonas", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Acinetobacter", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Salmonella", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Shigella", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Proteus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Citrobacter", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Serratia", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Morganella", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Providencia", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Stenotrophomonas", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Burkholderia", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Vibrio", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Haemophilus", StringComparison.OrdinalIgnoreCase) ||
            orgName.StartsWith("Neisseria", StringComparison.OrdinalIgnoreCase);

        bool isGP =
            string.Equals(gram, "Gram Positive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(gram, "GP", StringComparison.OrdinalIgnoreCase) ||
            isKnownGPOrg;

        bool isGN =
            string.Equals(gram, "Gram Negative", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(gram, "GN", StringComparison.OrdinalIgnoreCase) ||
            isKnownGNOrg;

        if (!string.IsNullOrWhiteSpace(gram))
        {
            if (isKnownGPOrg &&
                !gram.Contains("Positive", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(gram, "GP", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.GramReaction),
                    $"Gram Reaction '{gram}' conflicts with Gram-Positive organism '{orgName}'.");
            }
            else if (isKnownGNOrg &&
                     !gram.Contains("Negative", StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(gram, "GN", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.GramReaction),
                    $"Gram Reaction '{gram}' conflicts with Gram-Negative organism '{orgName}'.");
            }
            else if (isYeastOrg &&
                     (gram.Contains("Positive", StringComparison.OrdinalIgnoreCase) ||
                      gram.Contains("Negative", StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(
                    nameof(model.GramReaction),
                    $"'{orgName}' is a yeast/fungus and should not be classified as '{gram}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(morphology))
        {
            bool morphIsCocci = morphology.Contains("Cocci", StringComparison.OrdinalIgnoreCase);
            bool morphIsRods =
                morphology.Contains("Rod", StringComparison.OrdinalIgnoreCase) ||
                morphology.Contains("Bacill", StringComparison.OrdinalIgnoreCase);
            bool morphIsYeast =
                morphology.Contains("Yeast", StringComparison.OrdinalIgnoreCase) ||
                morphology.Contains("Budding", StringComparison.OrdinalIgnoreCase);

            if (isYeastOrg && (morphIsCocci || morphIsRods))
            {
                ModelState.AddModelError(
                    nameof(model.Morphology),
                    $"Morphology '{morphology}' is inconsistent with yeast organism '{orgName}'.");
            }

            if ((isGP || isGN) && morphIsYeast)
            {
                ModelState.AddModelError(
                    nameof(model.Morphology),
                    $"Morphology '{morphology}' is inconsistent with bacterial Gram reaction '{gram ?? orgName}'.");
            }

            if ((orgName.StartsWith("Staphylococcus", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Streptococcus", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Enterococcus", StringComparison.OrdinalIgnoreCase)) &&
                morphIsRods)
            {
                ModelState.AddModelError(
                    nameof(model.Morphology),
                    $"'{orgName}' is a coccus; morphology cannot be recorded as '{morphology}'.");
            }

            if ((orgName.StartsWith("Escherichia", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Klebsiella", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Pseudomonas", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Salmonella", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Shigella", StringComparison.OrdinalIgnoreCase) ||
                 orgName.StartsWith("Proteus", StringComparison.OrdinalIgnoreCase)) &&
                morphIsCocci &&
                !morphology.Contains("Coccobacill", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(
                    nameof(model.Morphology),
                    $"'{orgName}' is a rod/bacillus; morphology cannot be recorded as '{morphology}'.");
            }
        }

        if (isGP)
        {
            if (!IsBlankOrNotApplicable(lactose))
            {
                ModelState.AddModelError(
                    nameof(model.LactoseFermentation),
                    "Lactose fermentation is not applicable to Gram-Positive organisms.");
            }

            if (!IsBlankOrNotApplicable(oxidase))
            {
                ModelState.AddModelError(
                    nameof(model.Oxidase),
                    "Oxidase is not applicable to Gram-Positive organisms.");
            }
        }

        if (isGN)
        {
            if (!IsBlankOrNotApplicable(catalase))
            {
                ModelState.AddModelError(
                    nameof(model.Catalase),
                    "Catalase is not applicable to Gram-Negative organisms.");
            }

            if (!IsBlankOrNotApplicable(coagulase))
            {
                ModelState.AddModelError(
                    nameof(model.Coagulase),
                    "Coagulase is not applicable to Gram-Negative organisms.");
            }
        }
    }

    private void ValidateOrganismAndCardCompatibility(
        WorksheetViewModel model,
        List<string> selectedCards,
        CardCatalog cardCatalog)
    {
        var whonetID = Clean(model.WHONETID);

        if (selectedCards.Count == 0 || string.IsNullOrWhiteSpace(whonetID))
            return;

        foreach (var card in selectedCards)
        {
            if (!cardCatalog.AllowedOrganismsByCard.TryGetValue(card, out var allowedOrganisms))
                continue;

            if (allowedOrganisms.Count > 0 && !allowedOrganisms.Contains(whonetID))
            {
                ModelState.AddModelError(
                    nameof(model.ASTCards),
                    $"AST Card '{card}' is not mapped to organism '{whonetID}' in AST Card Configuration.");
            }
        }
    }

    private void ValidateAntibioticsAgainstCardsAndPairs(
        WorksheetViewModel model,
        List<string> selectedCards,
        CardCatalog cardCatalog)
    {
        if (model.Antibiotics == null || model.Antibiotics.Count == 0)
            return;

        var selectedCardSet = new HashSet<string>(
            selectedCards,
            StringComparer.OrdinalIgnoreCase);

        var allowedCodesFromDbCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unconfiguredCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in selectedCardSet)
        {
            if (cardCatalog.AntibioticSetByCard.TryGetValue(card, out var codes) &&
                codes.Count > 0)
            {
                allowedCodesFromDbCards.UnionWith(codes);
            }
            else
            {
                unconfiguredCards.Add(card);
            }
        }

        for (int i = 0; i < model.Antibiotics.Count; i++)
        {
            var abx = model.Antibiotics[i];
            var mic = Clean(abx.MIC);
            var sir = Clean(abx.Interpretation);

            bool hasMic = !string.IsNullOrWhiteSpace(mic);
            bool hasSir = !string.IsNullOrWhiteSpace(sir);

            if (!hasMic && !hasSir)
                continue;

            var label = string.IsNullOrWhiteSpace(abx.AntibioticName)
                ? abx.AntibioticCode
                : abx.AntibioticName;

            if (hasMic && !hasSir)
            {
                ModelState.AddModelError(
                    $"Antibiotics[{i}].Interpretation",
                    $"SIR interpretation is required for {label} when MIC ({mic}) is entered.");
            }
            else if (hasSir && abx.MICEnabled && !hasMic)
            {
                ModelState.AddModelError(
                    $"Antibiotics[{i}].MIC",
                    $"MIC value is required for {label} when SIR interpretation ({sir}) is selected.");
            }

            if (selectedCardSet.Count == 0)
            {
                ModelState.AddModelError(
                    nameof(model.ASTCards),
                    $"Cannot record results for {label} because no AST Card is selected.");
                continue;
            }

            var code = abx.AntibioticCode?.Trim() ?? string.Empty;

            bool matchesDbCard = allowedCodesFromDbCards.Contains(code);

            bool matchesLegacyCard =
                unconfiguredCards.Count > 0 &&
                !string.IsNullOrWhiteSpace(abx.ASTCard) &&
                abx.ASTCard
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Any(c => unconfiguredCards.Contains(c));

            if (!matchesDbCard && !matchesLegacyCard)
            {
                ModelState.AddModelError(
                    $"Antibiotics[{i}].MIC",
                    $"{label} does not belong to the selected AST Card(s) ({string.Join("; ", selectedCards)}).");
            }
        }
    }

    // =====================================================
    // DERIVED MDR / MRSA / CARBAPENEMASE VIA WORKSHEET SERVICE
    // =====================================================

    private Task<DerivedWorksheetClassification> CalculateDerivedForModelAsync(
        WorksheetViewModel model)
    {
        var interpretationsByCode = (model.Antibiotics ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.AntibioticCode))
            .GroupBy(a => a.AntibioticCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First().Interpretation,
                StringComparer.OrdinalIgnoreCase);

        return service.CalculateDerivedResultsAsync(
            model.WHONETID,
            model.CefoxitinScreen,
            interpretationsByCode);
    }

    // =====================================================
    // CEFOXITIN / OXACILLIN CONSISTENCY WARNING
    // =====================================================

    private void SetCefoxitinOxacillinWarning(
        WorksheetViewModel model)
    {
        if (!string.Equals(
                model.WHONETID?.Trim(),
                StaphylococcusAureus,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var screen = model.CefoxitinScreen?
            .Trim()
            .ToUpperInvariant();

        var oxacillin = model.Antibiotics?
            .FirstOrDefault(a =>
                string.Equals(
                    a.AntibioticCode?.Trim(),
                    "OXA",
                    StringComparison.OrdinalIgnoreCase))
            ?.Interpretation?
            .Trim()
            .ToUpperInvariant();

        bool mismatch =
            (screen == "POS" && oxacillin == "S") ||
            (screen == "NEG" && oxacillin == "R");

        if (mismatch)
        {
            TempData["Warning"] =
                "Cefoxitin Screen and Oxacillin interpretation disagree. " +
                "Please review the AST results before final validation.";
        }
    }

    // =====================================================
    // POPULATE STANDARD LOOKUPS
    // =====================================================

    private async Task PopulateLookups(WorksheetViewModel model)
    {
        model.WHONETIDs = await lookupService.GetOrganismsAsync();
    }

    // =====================================================
    // POPULATE AST CONFIGURATION FROM DATABASE CARD TABLES
    // =====================================================

    private async Task PopulateASTConfiguration(
        WorksheetViewModel model,
        CardCatalog cardCatalog)
    {
        var submittedResults = (model.Antibiotics
                ?? [])
            .Where(a => !string.IsNullOrWhiteSpace(a.AntibioticCode))
            .GroupBy(
                a => a.AntibioticCode.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    g.First().MIC,
                    g.First().Interpretation
                },
                StringComparer.OrdinalIgnoreCase);

        var legacyCards = await astConfigurationService.GetASTCardsAsync();

        var allCards = cardCatalog.CardsByCode.Keys
            .Concat(legacyCards)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        model.AvailableASTCards = allCards
            .Select(card => new SelectListItem
            {
                Value = card,
                Text = card
            })
            .ToList();

        ViewBag.CardOrganisms = allCards.ToDictionary(
            card => card,
            card => cardCatalog.AllowedOrganismsByCard.TryGetValue(card, out var orgs) && orgs.Count > 0
                ? string.Join(";", orgs)
                : string.Empty,
            StringComparer.OrdinalIgnoreCase);

        var allIntrinsicEntries = await context.IntrinsicResistances
            .AsNoTracking()
            .Select(r => new { r.WHONETID, r.AntibioticCode })
            .ToListAsync();

        var intrinsicLookupByOrganism = allIntrinsicEntries
            .Where(r => !string.IsNullOrWhiteSpace(r.WHONETID) &&
                        !string.IsNullOrWhiteSpace(r.AntibioticCode))
            .GroupBy(r => r.WHONETID.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.AntibioticCode.Trim().ToUpperInvariant())
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        ViewBag.IntrinsicResistancesJson =
            JsonSerializer.Serialize(intrinsicLookupByOrganism);

        var antibiotics =
            await astConfigurationService.GetActiveAntibioticsAsync();

        var whonetID = Clean(model.WHONETID);

        var intrinsicCodeSet = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (whonetID != null &&
            intrinsicLookupByOrganism.TryGetValue(whonetID, out var matchedIntrinsicCodes))
        {
            intrinsicCodeSet = new HashSet<string>(
                matchedIntrinsicCodes,
                StringComparer.OrdinalIgnoreCase);
        }

        var cardsByAntibioticCode = new Dictionary<string, HashSet<string>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (cardCode, abxCodes) in cardCatalog.OrderedAntibioticsByCard)
        {
            foreach (var abxCode in abxCodes)
            {
                if (!cardsByAntibioticCode.TryGetValue(abxCode, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    cardsByAntibioticCode[abxCode] = set;
                }
                set.Add(cardCode);
            }
        }

        var perCardOrdersByAntibioticCode = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (abxCode, matchedCards) in cardsByAntibioticCode)
        {
            var pairs = new List<string>();
            foreach (var cardCode in matchedCards.OrderBy(c => c))
            {
                if (cardCatalog.DisplayOrdersByCard.TryGetValue(cardCode, out var orderMap) &&
                    orderMap.TryGetValue(abxCode, out var ord))
                {
                    pairs.Add($"{cardCode}:{ord}");
                }
            }

            if (pairs.Count > 0)
            {
                perCardOrdersByAntibioticCode[abxCode] = string.Join(";", pairs);
            }
        }

        ViewBag.AntibioticCardOrders = perCardOrdersByAntibioticCode;

        model.Antibiotics = antibiotics.Select(a =>
        {
            var code = a.AntibioticCode.Trim();
            var matchedCards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (cardsByAntibioticCode.TryGetValue(code, out var dbMappedCards))
            {
                matchedCards.UnionWith(dbMappedCards);
            }

            if (matchedCards.Count == 0 && !string.IsNullOrWhiteSpace(a.ASTCard))
            {
                foreach (var c in a.ASTCard.Split(
                             ';',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    matchedCards.Add(c.ToUpperInvariant());
                }
            }

            var item = new WorksheetAntibioticViewModel
            {
                AntibioticCode = a.AntibioticCode,
                AntibioticName = a.AntibioticName,
                AntibioticClassName = a.AntibioticClassName,
                ASTCard = matchedCards.Count > 0
                    ? string.Join(";", matchedCards)
                    : a.ASTCard,
                ASTCards = [.. matchedCards],
                MICEnabled = a.MICEnabled,
                InterpretationEnabled = a.InterpretationEnabled,
                IsIntrinsicallyResistant =
                    intrinsicCodeSet.Contains(a.AntibioticCode)
            };

            if (!string.IsNullOrWhiteSpace(code) &&
                submittedResults.TryGetValue(code, out var saved))
            {
                item.MIC = saved.MIC;
                item.Interpretation = saved.Interpretation;
            }

            return item;
        }).ToList();

        ApplyDatabaseCardOrdering(model, cardCatalog);
    }

    private static void ApplyDatabaseCardOrdering(
        WorksheetViewModel model,
        CardCatalog cardCatalog)
    {
        if (model.Antibiotics == null || model.Antibiotics.Count == 0)
            return;

        var selectedCards = NormalizeASTCards(model.ASTCards);
        bool useSingleCardCustomOrder = selectedCards.Count == 1;
        string singleCard = useSingleCardCustomOrder ? selectedCards[0] : string.Empty;

        foreach (var abx in model.Antibiotics)
        {
            var code = abx.AntibioticCode.Trim();
            int order = int.MaxValue;

            if (useSingleCardCustomOrder &&
                cardCatalog.DisplayOrdersByCard.TryGetValue(singleCard, out var orderMap) &&
                orderMap.TryGetValue(code, out var configuredOrder))
            {
                order = configuredOrder;
            }

            abx.CardDisplayOrder = order;
        }

        model.Antibiotics = useSingleCardCustomOrder
            ? model.Antibiotics
                .OrderBy(a => a.CardDisplayOrder)
                .ThenBy(a => a.AntibioticName, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : model.Antibiotics
                .OrderBy(a => a.AntibioticName, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static void NormalizeWorksheetModel(WorksheetViewModel model)
    {
        if (!string.IsNullOrWhiteSpace(model.PatientID))
        {
            model.PatientID = model.PatientID.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(model.IsolateID))
        {
            model.IsolateID = model.IsolateID.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrWhiteSpace(model.ManuscriptID))
        {
            model.ManuscriptID = model.ManuscriptID.Trim();
        }

        model.ASTCards = NormalizeASTCards(model.ASTCards);
    }

    private static bool IsNoGrowth(string? cultureResult)
    {
        var val = cultureResult?.Trim();
        return string.Equals(val, "No Growth", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "NSG", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(val, "NG48", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGrowth(string? cultureResult)
    {
        return string.Equals(
            cultureResult?.Trim(),
            "Growth",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlankOrNotApplicable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var trimmed = value.Trim();
        return string.Equals(trimmed, "N/A", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(trimmed, "Not Applicable", StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearGrowthSpecificFields(WorksheetViewModel model)
    {
        model.AcceptedID = null;
        model.WHONETID = null;
        model.GramReaction = null;
        model.Morphology = null;
        model.Hemolysis = null;
        model.Catalase = null;
        model.Oxidase = null;
        model.Coagulase = null;
        model.LactoseFermentation = null;
        model.ASTCards = [];
        model.ESBL = null;
        model.MDR = null;
        model.Carbapenemase = null;
        model.MRSA = null;
        model.CefoxitinScreen = null;
        model.InducibleClindamycinResistance = null;
        model.ASTComments = null;

        if (model.Antibiotics != null)
        {
            foreach (var abx in model.Antibiotics)
            {
                abx.MIC = null;
                abx.Interpretation = null;
            }
        }
    }

    private static List<string> NormalizeASTCards(IEnumerable<string>? astCards)
    {
        if (astCards == null)
            return [];

        return astCards
            .SelectMany(c => c.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseASTCards(string? astCard)
    {
        if (string.IsNullOrWhiteSpace(astCard))
            return [];

        return astCard.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(c => c.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? Clean(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}