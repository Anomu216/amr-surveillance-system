using AMRSurveillance.Extensions;
using AMRSurveillance.Imports.Interfaces;
using AMRSurveillance.Imports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Imports.Services;

public class ImportWorkflowService<TEntity> : IImportWorkflow<TEntity>
    where TEntity : class
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IExcelImportService _excelImportService;
    private readonly IEntityImportValidator<TEntity> _validator;
    private readonly IImportService<TEntity> _importService;
    private readonly IImportTemplateService _templateService;

    public ImportWorkflowService(
        IHttpContextAccessor httpContextAccessor,
        IExcelImportService excelImportService,
        IEntityImportValidator<TEntity> validator,
        IImportService<TEntity> importService,
        IImportTemplateService templateService)
    {
        _httpContextAccessor = httpContextAccessor;
        _excelImportService = excelImportService;
        _validator = validator;
        _importService = importService;
        _templateService = templateService;
    }

    public async Task<ImportViewModel<TEntity>> ValidateAsync(
        IFormFile file,
        List<string> requiredHeaders,
        string sessionKey)
    {
        var model = new ImportViewModel<TEntity>
        {
            File = file
        };

        if (file == null || file.Length == 0)
        {
            model.Errors.Add(new ImportRowError
            {
                RowNumber = 0,
                ColumnName = string.Empty,
                Message = "Please select an Excel file."
            });

            model.Summary = BuildSummary(model);

            return model;
        }

        try
        {
            var worksheet =
                await _excelImportService.ReadWorksheetAsync(file);

            var headers =
                _excelImportService.GetHeaders(worksheet);

            var isValid =
                _excelImportService.ValidateHeaders(
                    headers,
                    requiredHeaders,
                    out var missingHeaders,
                    out var extraHeaders);

            if (!isValid)
            {
                foreach (var header in missingHeaders)
                {
                    model.Errors.Add(new ImportRowError
                    {
                        RowNumber = 1,
                        ColumnName = header,
                        Message =
                            $"Required column '{header}' is missing."
                    });
                }

                foreach (var header in extraHeaders)
                {
                    model.Errors.Add(new ImportRowError
                    {
                        RowNumber = 1,
                        ColumnName = header,
                        Message =
                            $"Unexpected column '{header}' was found."
                    });
                }

                model.IsValidated = true;
                model.Summary = BuildSummary(model);

                return model;
            }

            var validationResult =
                await _validator.ValidateAsync(worksheet);

            model.Records =
                validationResult.Records;

            model.Errors =
                validationResult.Errors;

            model.Warnings =
                 validationResult.Warnings;

            model.Summary =
                validationResult.Summary;

            model.IsValidated = true;

            if (model.ReadyToImport)
            {
                SetSessionRecords(
                    sessionKey,
                    model.Records);
            }
            else
            {
                ClearSession(sessionKey);
            }

            return model;
        }
        catch (Exception ex)
        {
            model.Errors.Add(new ImportRowError
            {
                RowNumber = 0,
                ColumnName = string.Empty,
                Message =
                    $"Import validation failed: {ex.Message}"
            });

            model.IsValidated = true;
            model.Summary = BuildSummary(model);

            ClearSession(sessionKey);

            return model;
        }
    }

    public async Task<ImportSummary> ConfirmImportAsync(string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new ArgumentException(
                "A session key is required.",
                nameof(sessionKey));
        }

        var records = GetSessionRecords(sessionKey);

        if (records == null || records.Count == 0)
        {
            return new ImportSummary();
        }

        await _importService.CreateRangeAsync(records);

        var summary = new ImportSummary
        {
            TotalRows = records.Count,
            SuccessfulRows = records.Count,
            FailedRows = 0
        };

        ClearSession(sessionKey);

        return summary;
    }

    public async Task<FileContentResult> DownloadTemplateAsync(
        IReadOnlyList<ImportFieldDefinition> fields,
        string templateFileName)
    {
        return await _templateService.GenerateTemplateAsync(
            fields,
            templateFileName);
    }

    public void ClearSession(string sessionKey)
    {
        _httpContextAccessor
            .HttpContext?
            .Session
            .Remove(sessionKey);
    }

    private void SetSessionRecords(
        string sessionKey,
        IEnumerable<TEntity> records)
    {
        _httpContextAccessor
            .HttpContext?
            .Session
            .SetObject(
                sessionKey,
                records.ToList());
    }

    private List<TEntity>? GetSessionRecords(
        string sessionKey)
    {
        return _httpContextAccessor
            .HttpContext?
            .Session
            .GetObject<List<TEntity>>(sessionKey);
    }

    private static ImportSummary BuildSummary(
        ImportViewModel<TEntity> model)
    {
        var failedRows =
            model.Errors
                .Where(e => e.RowNumber > 0)
                .Select(e => e.RowNumber)
                .Distinct()
                .Count();

        return new ImportSummary
        {
            TotalRows =
                model.Records.Count + failedRows,

            SuccessfulRows =
                model.Records.Count,

            FailedRows =
                failedRows
        };
    }
}