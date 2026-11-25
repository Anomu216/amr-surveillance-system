using AMRSurveillance.Imports.Models;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Imports.Interfaces;

public interface IImportTemplateService
{
    Task<FileContentResult> GenerateTemplateAsync(
        IReadOnlyList<ImportFieldDefinition> fields,
        string templateName);
}