using AMRSurveillance.Imports.Interfaces;
using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Services;

public class ExcelImportService : IExcelImportService
{
    // =====================================================
    // READ WORKSHEET
    // =====================================================

    public async Task<IXLWorksheet> ReadWorksheetAsync(
        IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException(
                "No file was uploaded.");
        }

        if (!Path.GetExtension(file.FileName)
            .Equals(
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Only Excel (.xlsx) files are supported.");
        }

        using var stream = new MemoryStream();

        await file.CopyToAsync(stream);

        stream.Position = 0;

        var workbook = new XLWorkbook(stream);

        if (!workbook.Worksheets.Any())
        {
            throw new ArgumentException(
                "The Excel workbook does not contain a worksheet.");
        }

        return workbook.Worksheet(1);
    }


    // =====================================================
    // GET HEADERS
    // =====================================================

    public List<string> GetHeaders(
        IXLWorksheet worksheet)
    {
        if (worksheet == null)
        {
            throw new ArgumentNullException(
                nameof(worksheet));
        }

        return worksheet
            .Row(1)
            .CellsUsed()
            .Select(c => c.GetString().Trim())
            .ToList();
    }


    // =====================================================
    // VALIDATE HEADERS
    // =====================================================

    public bool ValidateHeaders(
        List<string> headers,
        List<string> requiredHeaders,
        out List<string> missingHeaders,
        out List<string> extraHeaders)
    {
        missingHeaders = requiredHeaders
            .Except(
                headers,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        extraHeaders = headers
            .Except(
                requiredHeaders,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        return
            !missingHeaders.Any() &&
            !extraHeaders.Any();
    }
}