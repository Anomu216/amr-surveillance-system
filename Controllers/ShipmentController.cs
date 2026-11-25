
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers;


[Authorize(Roles = AppRoles.SystemAdministrator)]
public class ShipmentController : Controller
{
    private readonly IShipmentService _service;

    private const string ViewPath = "~/Views/Shipment/";


    public ShipmentController(IShipmentService service)
    {
        _service = service;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var shipments = await _service.GetAllAsync();

        var model = shipments
            .Select(s => new ShipmentViewModel
            {
                KENID = s.KENID,
                IsolateID = s.IsolateID,
                MALDIID = s.MALDIID,
                MRSNID = s.MRSNID,
                ShipmentDate = s.ShipmentDate,
                ShipmentNumber = s.ShipmentNumber,
                BoxNumber = s.BoxNumber
            })
            .ToList();

        return View($"{ViewPath}Index.cshtml", model);
    }

    // =========================================================
    // DETAILS
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Details(string kenID)
    {
        if (string.IsNullOrWhiteSpace(kenID))
            return NotFound();

        var shipment = await _service.GetByIdAsync(kenID);

        if (shipment == null)
            return NotFound();

        var model = MapToViewModel(shipment);

        return View($"{ViewPath}Details.cshtml", model);
    }

    // =========================================================
    // CREATE - GET
    // =========================================================

    [HttpGet]
    public IActionResult Create()
    {
        return View(
            $"{ViewPath}Create.cshtml",
            new ShipmentViewModel());
    }

    // =========================================================
    // CREATE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShipmentViewModel model)
    {
        NormalizeModel(model);

        if (!ModelState.IsValid)
        {
            return View($"{ViewPath}Create.cshtml", model);
        }

        if (await _service.ExistsAsync(model.KENID))
        {
            ModelState.AddModelError(
                nameof(model.KENID),
                "A shipment with this KENID already exists.");

            return View($"{ViewPath}Create.cshtml", model);
        }

        if (!await _service.WorksheetExistsAsync(model.IsolateID))
        {
            ModelState.AddModelError(
                nameof(model.IsolateID),
                "The specified IsolateID does not exist in Worksheets.");

            return View($"{ViewPath}Create.cshtml", model);
        }

        if (!string.IsNullOrWhiteSpace(model.MRSNID) &&
            await _service.MRSNIDExistsAsync(model.MRSNID))
        {
            ModelState.AddModelError(
                nameof(model.MRSNID),
                "This MRSNID is already assigned to another shipment.");

            return View($"{ViewPath}Create.cshtml", model);
        }

        var shipment = MapToEntity(model);

        try
        {
            await _service.CreateAsync(shipment);

            TempData["Success"] = "Shipment created successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The shipment could not be saved. Check that KENID and MRSNID are unique and IsolateID exists.");

            return View($"{ViewPath}Create.cshtml", model);
        }
    }

    // =========================================================
    // EDIT - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Edit(string kenID)
    {
        if (string.IsNullOrWhiteSpace(kenID))
            return NotFound();

        var shipment = await _service.GetByIdAsync(kenID);

        if (shipment == null)
            return NotFound();

        return View(
            $"{ViewPath}Edit.cshtml",
            MapToViewModel(shipment));
    }

    // =========================================================
    // EDIT - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string kenID,
        ShipmentViewModel model)
    {
        NormalizeModel(model);

        if (!string.Equals(
                kenID,
                model.KENID,
                StringComparison.Ordinal))
        {
            ModelState.AddModelError(
                nameof(model.KENID),
                "The shipment identifier cannot be changed.");
        }

        var existing = await _service.GetByIdAsync(kenID);

        if (existing == null)
            return NotFound();

        if (!ModelState.IsValid)
        {
            return View($"{ViewPath}Edit.cshtml", model);
        }

        if (!await _service.WorksheetExistsAsync(model.IsolateID))
        {
            ModelState.AddModelError(
                nameof(model.IsolateID),
                "The specified IsolateID does not exist in Worksheets.");

            return View($"{ViewPath}Edit.cshtml", model);
        }

        if (!string.IsNullOrWhiteSpace(model.MRSNID) &&
            await _service.MRSNIDExistsAsync(
                model.MRSNID,
                existing.KENID))
        {
            ModelState.AddModelError(
                nameof(model.MRSNID),
                "This MRSNID is already assigned to another shipment.");

            return View($"{ViewPath}Edit.cshtml", model);
        }

        existing.IsolateID = model.IsolateID;
        existing.MALDIID = model.MALDIID;
        existing.MRSNID = model.MRSNID;
        existing.ShipmentDate = model.ShipmentDate;
        existing.ShipmentNumber = model.ShipmentNumber;
        existing.BoxNumber = model.BoxNumber;

        try
        {
            await _service.UpdateAsync(existing);

            TempData["Success"] = "Shipment updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The shipment could not be updated. Check the identifiers and worksheet.");

            return View($"{ViewPath}Edit.cshtml", model);
        }
    }

    // =========================================================
    // EXCEL TEMPLATE
    // =========================================================

    [HttpGet]
    public IActionResult Template()
    {
        using var workbook = new XLWorkbook();

        var worksheet = workbook.Worksheets.Add("Shipments");

        var headers = new[]
        {
            "KENID",
            "IsolateID",
            "MALDIID",
            "MRSNID",
            "ShipmentDate",
            "ShipmentNumber",
            "BoxNumber"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            worksheet.Cell(1, i + 1).Value = headers[i];
            worksheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Shipment_Template.xlsx");
    }

    // =========================================================
    // IMPORT - GET
    // =========================================================

    [HttpGet]
    public IActionResult Import()
    {
        return View($"{ViewPath}Import.cshtml");
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
            TempData["Error"] = "Please select an Excel file to import.";
            return RedirectToAction(nameof(Import));
        }

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only Excel (.xlsx) files are supported.";
            return RedirectToAction(nameof(Import));
        }

        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
            {
                TempData["Error"] = "The workbook contains no worksheet.";
                return RedirectToAction(nameof(Import));
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastColumn = worksheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;

            if (lastRow < 2 || lastColumn == 0)
            {
                TempData["Error"] = "The workbook contains no shipment data.";
                return RedirectToAction(nameof(Import));
            }

            var expectedHeaders = new[]
            {
                "KENID",
                "IsolateID",
                "MALDIID",
                "MRSNID",
                "ShipmentDate",
                "ShipmentNumber",
                "BoxNumber"
            };

            var headerMap = new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

            for (var column = 1; column <= lastColumn; column++)
            {
                var header = worksheet.Cell(1, column).GetString().Trim();

                if (!string.IsNullOrWhiteSpace(header))
                {
                    if (!headerMap.TryAdd(header, column))
                    {
                        TempData["Error"] =
                            $"The workbook contains a duplicate column header: {header}.";
                        return RedirectToAction(nameof(Import));
                    }
                }
            }

            var missingHeaders = expectedHeaders
                .Where(h => !headerMap.ContainsKey(h))
                .ToList();

            if (missingHeaders.Count > 0)
            {
                TempData["Error"] =
                    $"Missing required column(s): {string.Join(", ", missingHeaders)}.";
                return RedirectToAction(nameof(Import));
            }

            var seenKENIDs = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            var seenMRSNIDs = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            // Read and validate the entire file before saving.
            var pending = new List<Shipment>();

            for (var row = 2; row <= lastRow; row++)
            {
                string Cell(string header) =>
                    worksheet.Cell(row, headerMap[header]).GetString().Trim();

                var kenID = Cell("KENID");
                var isolateID = Cell("IsolateID");
                var maldiID = Cell("MALDIID");
                var mrsnID = Cell("MRSNID");
                var shipmentNumber = Cell("ShipmentNumber");
                var boxNumber = Cell("BoxNumber");
                var shipmentDateCell = worksheet.Cell(row, headerMap["ShipmentDate"]);

                if (string.IsNullOrWhiteSpace(kenID) &&
                    string.IsNullOrWhiteSpace(isolateID))
                {
                    continue;
                }

                var rowErrors = new List<string>();

                if (string.IsNullOrWhiteSpace(kenID))
                    rowErrors.Add("KENID is required.");
                else if (kenID.Length > 150)
                    rowErrors.Add("KENID exceeds 150 characters.");
                else if (!seenKENIDs.Add(kenID))
                    rowErrors.Add("KENID is duplicated in the uploaded file.");

                if (string.IsNullOrWhiteSpace(isolateID))
                    rowErrors.Add("IsolateID is required.");
                else if (isolateID.Length > 20)
                    rowErrors.Add("IsolateID exceeds 20 characters.");

                if (maldiID.Length > 150)
                    rowErrors.Add("MALDIID exceeds 150 characters.");

                if (mrsnID.Length > 150)
                    rowErrors.Add("MRSNID exceeds 150 characters.");

                if (!string.IsNullOrWhiteSpace(mrsnID))
                {
                    if (!seenMRSNIDs.Add(mrsnID))
                        rowErrors.Add("MRSNID is duplicated in the uploaded file.");
                }

                if (shipmentNumber.Length > 100)
                    rowErrors.Add("ShipmentNumber exceeds 100 characters.");

                if (boxNumber.Length > 100)
                    rowErrors.Add("BoxNumber exceeds 100 characters.");

                DateTime? shipmentDate = null;

                if (!shipmentDateCell.IsEmpty())
                {
                    if (shipmentDateCell.TryGetValue<DateTime>(out var parsedDate))
                    {
                        shipmentDate = parsedDate.Date;
                    }
                    else if (DateTime.TryParse(
                        shipmentDateCell.GetString(),
                        out parsedDate))
                    {
                        shipmentDate = parsedDate.Date;
                    }
                    else
                    {
                        rowErrors.Add("ShipmentDate is not a valid date.");
                    }
                }

                if (rowErrors.Count > 0)
                {
                    skipped++;
                    errors.Add($"Row {row}: {string.Join(" ", rowErrors)}");
                    continue;
                }

                if (await _service.ExistsAsync(kenID))
                {
                    skipped++;
                    errors.Add($"Row {row}: KENID '{kenID}' already exists.");
                    continue;
                }

                if (!await _service.WorksheetExistsAsync(isolateID))
                {
                    skipped++;
                    errors.Add($"Row {row}: IsolateID '{isolateID}' does not exist.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mrsnID) &&
                    await _service.MRSNIDExistsAsync(mrsnID))
                {
                    skipped++;
                    errors.Add($"Row {row}: MRSNID '{mrsnID}' already exists.");
                    continue;
                }

                pending.Add(new Shipment
                {
                    KENID = kenID,
                    IsolateID = isolateID,
                    MALDIID = EmptyToNull(maldiID),
                    MRSNID = EmptyToNull(mrsnID),
                    ShipmentDate = shipmentDate,
                    ShipmentNumber = EmptyToNull(shipmentNumber),
                    BoxNumber = EmptyToNull(boxNumber)
                });
            }

            // Save rows individually using the existing service.
            // Invalid or duplicate rows do not prevent valid rows from importing.
            foreach (var shipment in pending)
            {
                try
                {
                    await _service.CreateAsync(shipment);
                    imported++;
                }
                catch (DbUpdateException)
                {
                    skipped++;
                    errors.Add(
                        $"KENID '{shipment.KENID}': database rejected the record, possibly because an identifier was duplicated.");
                }
            }
        }
        catch (Exception ex) when (
            ex is not OutOfMemoryException &&
            ex is not StackOverflowException)
        {
            TempData["Error"] =
                $"Import failed while reading the workbook: {ex.Message}";
            return RedirectToAction(nameof(Import));
        }

        TempData["Success"] =
            $"Import completed. {imported} shipment(s) imported and {skipped} row(s) skipped.";

        if (errors.Count > 0)
        {
            TempData["ImportErrors"] = string.Join(Environment.NewLine, errors.Take(30));
            TempData["ImportErrorsTruncated"] = errors.Count > 30;
        }

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // DELETE - GET
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Delete(string kenID)
    {
        if (string.IsNullOrWhiteSpace(kenID))
            return NotFound();

        var shipment = await _service.GetByIdAsync(kenID);

        if (shipment == null)
            return NotFound();

        var model = MapToViewModel(shipment);

        return View($"{ViewPath}Delete.cshtml", model);
    }

    // =========================================================
    // DELETE - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string kenID)
    {
        if (string.IsNullOrWhiteSpace(kenID))
            return NotFound();

        try
        {
            var shipment = await _service.GetByIdAsync(kenID);

            if (shipment == null)
            {
                TempData["Error"] = "Shipment was not found.";
                return RedirectToAction(nameof(Index));
            }

            await _service.DeleteAsync(kenID);

            TempData["Success"] = "Shipment deleted successfully.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] =
                "This shipment cannot be deleted because it is referenced by a genomics record.";
        }

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // MAPPING / NORMALIZATION
    // =========================================================

    private static ShipmentViewModel MapToViewModel(Shipment shipment)
    {
        return new ShipmentViewModel
        {
            KENID = shipment.KENID,
            IsolateID = shipment.IsolateID,
            MALDIID = shipment.MALDIID,
            MRSNID = shipment.MRSNID,
            ShipmentDate = shipment.ShipmentDate,
            ShipmentNumber = shipment.ShipmentNumber,
            BoxNumber = shipment.BoxNumber
        };
    }

    private static Shipment MapToEntity(ShipmentViewModel model)
    {
        return new Shipment
        {
            KENID = model.KENID,
            IsolateID = model.IsolateID,
            MALDIID = model.MALDIID,
            MRSNID = model.MRSNID,
            ShipmentDate = model.ShipmentDate,
            ShipmentNumber = model.ShipmentNumber,
            BoxNumber = model.BoxNumber
        };
    }

    private static void NormalizeModel(ShipmentViewModel model)
    {
        model.KENID = model.KENID?.Trim() ?? string.Empty;
        model.IsolateID = model.IsolateID?.Trim() ?? string.Empty;
        model.MALDIID = EmptyToNull(model.MALDIID);
        model.MRSNID = EmptyToNull(model.MRSNID);
        model.ShipmentNumber = EmptyToNull(model.ShipmentNumber);
        model.BoxNumber = EmptyToNull(model.BoxNumber);
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
