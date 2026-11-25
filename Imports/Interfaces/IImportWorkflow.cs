using AMRSurveillance.Imports.Models;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Imports.Interfaces;

public interface IImportWorkflow<TEntity>
    where TEntity : class
{
    Task<ImportViewModel<TEntity>> ValidateAsync(
        IFormFile file,
        List<string> requiredHeaders,
        string sessionKey);

    Task<ImportSummary> ConfirmImportAsync(
        string sessionKey);

    Task<FileContentResult> DownloadTemplateAsync(
       IReadOnlyList<ImportFieldDefinition> fields,
       string templateFileName);

    void ClearSession(string sessionKey);
}