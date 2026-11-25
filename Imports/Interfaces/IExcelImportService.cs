using ClosedXML.Excel;

namespace AMRSurveillance.Imports.Interfaces;

public interface IExcelImportService
{
    Task<IXLWorksheet> ReadWorksheetAsync(
        IFormFile file);

    List<string> GetHeaders(
        IXLWorksheet worksheet);

    bool ValidateHeaders(
        List<string> headers,
        List<string> requiredHeaders,
        out List<string> missingHeaders,
        out List<string> extraHeaders);
}