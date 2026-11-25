using System.IO;
using AMRSurveillance.ViewModels;

namespace AMRSurveillance.Imports.Interfaces;

public interface IWorksheetImportService
{
    Task<byte[]> GenerateTemplateAsync();

    Task<WorksheetImportResult> ImportAsync(
        Stream fileStream);
}