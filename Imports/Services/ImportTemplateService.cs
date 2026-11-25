using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Imports.Services;

public class ImportTemplateService : IImportTemplateService
{
    private readonly IImportLookupService _lookupService;

    // Number of rows available for data entry in the template.
    private const int TemplateRowLimit = 1000;

    public ImportTemplateService(
        IImportLookupService lookupService)
    {
        _lookupService = lookupService;
    }

    public async Task<FileContentResult> GenerateTemplateAsync(
        IReadOnlyList<ImportFieldDefinition> fields,
        string templateName)
    {
        using var workbook = new XLWorkbook();

        var worksheet =
            workbook.Worksheets.Add("Import");

        var lookupWorksheet =
            workbook.Worksheets.Add("LookupData");

        lookupWorksheet.Visibility =
            XLWorksheetVisibility.VeryHidden;

        // =========================================================
        // MAIN WORKSHEET HEADERS
        // =========================================================

        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];

            var cell =
                worksheet.Cell(1, i + 1);

            cell.Value = field.Name;

            cell.Style.Font.Bold = true;

            cell.Style.Fill.BackgroundColor =
                XLColor.LightBlue;

            cell.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            cell.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            if (field.Required)
            {
                cell.Style.Font.FontColor =
                    XLColor.Red;
            }
        }

        worksheet.Row(1).Height = 25;

        worksheet.SheetView.FreezeRows(1);


        // =========================================================
        // LOOKUP VALUES AND DROPDOWN VALIDATION
        // =========================================================

        int lookupColumn = 1;

        for (int fieldIndex = 0;
             fieldIndex < fields.Count;
             fieldIndex++)
        {
            var field = fields[fieldIndex];

            if (string.IsNullOrWhiteSpace(
                    field.LookupName))
            {
                continue;
            }

            var values =
                (await _lookupService
                    .GetValuesAsync(field.LookupName))
                .Select(x => x.Value?.Trim())
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!values.Any())
            {
                continue;
            }


            // -----------------------------------------------------
            // STORE LOOKUP VALUES ON HIDDEN WORKSHEET
            // -----------------------------------------------------

            for (int row = 0;
                 row < values.Count;
                 row++)
            {
                lookupWorksheet
                    .Cell(row + 1, lookupColumn)
                    .Value = values[row];
            }


            // -----------------------------------------------------
            // CREATE A NAMED RANGE FOR THIS LOOKUP LIST
            // -----------------------------------------------------

            var firstCell =
                lookupWorksheet.Cell(
                    1,
                    lookupColumn);

            var lastCell =
                lookupWorksheet.Cell(
                    values.Count,
                    lookupColumn);

            var lookupRange =
                lookupWorksheet.Range(
                    firstCell,
                    lastCell);

            // Create a unique Excel-safe name for the lookup.
            var rangeName =
                $"Lookup_{fieldIndex + 1}";

            lookupRange.AddToNamed(
                rangeName,
                XLScope.Workbook);


            // -----------------------------------------------------
            // APPLY THE SAME DROPDOWN TO THE ENTIRE COLUMN RANGE
            // -----------------------------------------------------

            var validationRange =
                worksheet.Range(
                    2,
                    fieldIndex + 1,
                    TemplateRowLimit,
                    fieldIndex + 1);

            var validation =
                validationRange
                    .CreateDataValidation();

            validation.IgnoreBlanks = true;

            validation.InCellDropdown = true;

            // Use the named range rather than a direct worksheet reference.
            validation.List(
                $"={rangeName}");


            lookupColumn++;
        }


        // =========================================================
        // FORMATTING
        // =========================================================

        worksheet.Columns()
            .AdjustToContents();

        for (int column = 1;
             column <= fields.Count;
             column++)
        {
            var width =
                worksheet.Column(column).Width;

            if (width < 15)
            {
                worksheet.Column(column).Width = 15;
            }

            if (width > 35)
            {
                worksheet.Column(column).Width = 35;
            }
        }

        worksheet.Row(2).Height = 20;


        // =========================================================
        // SAVE WORKBOOK
        // =========================================================

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return new FileContentResult(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            FileDownloadName = templateName
        };
    }
}