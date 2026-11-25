using System.Security.Claims;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels.DataRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

public class DataRequestController(
    IDataRequestService dataRequestService,
    IAuditService auditService,
    ILogger<DataRequestController> logger) : Controller
{
    // ============================================================
    // REQUEST HOME
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var model =
            await dataRequestService
                .GetDataRequestViewModelAsync(
                    cancellationToken);

        return View(model);
    }

    // ============================================================
    // VALIDATE IDENTIFIER FILE
    //
    // Invalid ID format = error.
    // Valid format but not found = warning.
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateIdentifiers(
        DataRequestViewModel model,
        CancellationToken cancellationToken)
    {
        var pageModel =
            await dataRequestService
                .GetDataRequestViewModelAsync(
                    cancellationToken);

        CopyRequestValues(
            model,
            pageModel);

        if (string.IsNullOrWhiteSpace(
                model.IdentifierType))
        {
            ModelState.AddModelError(
                nameof(model.IdentifierType),
                "Please select an identifier type.");
        }

        if (model.IdentifierFile is null ||
            model.IdentifierFile.Length == 0)
        {
            ModelState.AddModelError(
                nameof(model.IdentifierFile),
                "Please select an Excel file containing identifiers.");
        }

        if (!ModelState.IsValid)
        {
            return View(
                "Index",
                pageModel);
        }

        try
        {
            var validationResults =
                await dataRequestService
                    .ValidateIdentifierFileAsync(
                        model.IdentifierType!,
                        model.IdentifierFile!,
                        cancellationToken);

            pageModel.IdentifierValidationResults =
                validationResults;

            if (pageModel.HasIdentifierValidationErrors)
            {
                pageModel.ErrorMessage =
                    "Some identifiers have invalid formats. " +
                    "Please correct the source file before submitting the request.";
            }
            else if (pageModel.MissingIdentifierCount > 0)
            {
                pageModel.SuccessMessage =
                    "Identifier validation completed. Some identifiers " +
                    "have valid formats but were not found in the database. " +
                    "You may still submit the request.";
            }
            else
            {
                pageModel.SuccessMessage =
                    "All identifiers were successfully validated.";
            }
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(
                string.Empty,
                ex.Message);
        }

        return View(
            "Index",
            pageModel);
    }

    // ============================================================
    // SUBMIT DATA REQUEST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        DataRequestViewModel model,
        CancellationToken cancellationToken)
    {
        var pageModel =
            await dataRequestService
                .GetDataRequestViewModelAsync(
                    cancellationToken);

        CopyRequestValues(
            model,
            pageModel);

        // Require at least one selected column.
        var hasSelectedColumns =
            pageModel.AvailableDatasets
                .Any(dataset =>
                    dataset.Columns.Any(
                        column => column.IsSelected));

        if (!hasSelectedColumns)
        {
            ModelState.AddModelError(
                string.Empty,
                "Please select at least one column to request.");
        }

        // A reason is required.
        if (string.IsNullOrWhiteSpace(
                pageModel.Reason))
        {
            ModelState.AddModelError(
                nameof(model.Reason),
                "Please provide a reason for the data request.");
        }

        // Block submission if an identifier file was uploaded (or identifier type chosen)
        // without running identifier validation first.
        var hasUploadedFile = pageModel.IdentifierFile is { Length: > 0 };
        var hasIdentifierType = !string.IsNullOrWhiteSpace(pageModel.IdentifierType);
        var hasValidatedResults = pageModel.IdentifierValidationResults.Count > 0;

        if ((hasUploadedFile || hasIdentifierType) && !hasValidatedResults)
        {
            ModelState.AddModelError(
                nameof(model.IdentifierFile),
                "You selected an identifier filter or uploaded an identifier file without validating it. " +
                "Please select your Excel file and click \"Validate IDs\" before submitting so the export is restricted to your requested IDs.");
        }

        // Invalid identifier patterns block submission.
        if (pageModel.HasIdentifierValidationErrors)
        {
            ModelState.AddModelError(
                string.Empty,
                "The request contains identifiers with invalid formats. " +
                "Please correct and validate the identifier file before submitting.");
        }

        if (!ModelState.IsValid)
        {
            return View(
                "Index",
                pageModel);
        }

        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            var requestId =
                await dataRequestService
                    .CreateDataRequestAsync(
                        pageModel,
                        userId,
                        cancellationToken);

            await auditService.LogAsync(
                action: "Create",
                entityName: nameof(DataRequest),
                entityKey: requestId.ToString(),
                summary: hasValidatedResults
                    ? $"Submitted data request #{requestId} filtered by {pageModel.IdentifierValidationResults.Count} validated {pageModel.IdentifierType} identifier(s)"
                    : $"Submitted data request #{requestId} (cohort-wide filter)",
                cancellationToken: cancellationToken);

            TempData["SuccessMessage"] =
                $"Your data request #{requestId} was submitted successfully " +
                "and is awaiting review.";

            return RedirectToAction(
                nameof(MyRequests));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(
                string.Empty,
                ex.Message);

            return View(
                "Index",
                pageModel);
        }
    }

    // ============================================================
    // MY REQUESTS
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> MyRequests(
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var requests =
            await dataRequestService
                .GetUserRequestsAsync(
                    userId,
                    cancellationToken);

        return View(requests);
    }

    // ============================================================
    // COPY USER INPUT TO RELOADED PAGE MODEL
    // ============================================================

    private static void CopyRequestValues(
        DataRequestViewModel source,
        DataRequestViewModel destination)
    {
        destination.Reason =
            source.Reason ?? string.Empty;

        destination.StartDate =
            source.StartDate;

        destination.EndDate =
            source.EndDate;

        destination.FacilityIDs =
            source.FacilityIDs ?? [];

        destination.OrganismIDs =
            source.OrganismIDs ?? [];

        destination.IdentifierType =
            source.IdentifierType;

        destination.IdentifierFile =
            source.IdentifierFile;

        destination.IdentifierValidationResults =
            source.IdentifierValidationResults ?? [];

        foreach (var sourceDataset
                 in source.AvailableDatasets)
        {
            var destinationDataset =
                destination.AvailableDatasets
                    .FirstOrDefault(
                        x => string.Equals(
                            x.Value,
                            sourceDataset.Value,
                            StringComparison.OrdinalIgnoreCase));

            if (destinationDataset is null)
            {
                continue;
            }

            foreach (var sourceColumn
                     in sourceDataset.Columns)
            {
                var destinationColumn =
                    destinationDataset.Columns
                        .FirstOrDefault(
                            x => string.Equals(
                                x.Value,
                                sourceColumn.Value,
                                StringComparison.OrdinalIgnoreCase));

                if (destinationColumn is not null)
                {
                    destinationColumn.IsSelected =
                        sourceColumn.IsSelected;
                }
            }
        }
    }

    // ============================================================
    // DOWNLOAD APPROVED DATA REQUEST
    // ============================================================

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Download(
        int id,
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var dataRequest =
            await dataRequestService
                .GetDataRequestByIdAsync(
                    id,
                    cancellationToken);

        if (dataRequest is null)
        {
            return NotFound();
        }

        if (!string.Equals(
                dataRequest.RequestedByUserID,
                userId,
                StringComparison.Ordinal))
        {
            return Forbid();
        }

        if (!string.Equals(
                dataRequest.Status,
                "Approved",
                StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] =
                "This data request has not been approved for download.";

            return RedirectToAction(
                nameof(MyRequests));
        }

        try
        {
            var excelFile =
                await dataRequestService
                    .GenerateExcelExportAsync(
                        dataRequest,
                        cancellationToken);

            await dataRequestService
                .MarkRequestAsExportedAsync(
                    dataRequest.DataRequestID,
                    cancellationToken);

            var fileName =
                $"DataRequest_{dataRequest.DataRequestID}_{EastAfricaTime.Now:yyyyMMdd_HHmmss}_EAT.xlsx";

            await auditService.LogAsync(
                action: "Export",
                entityName: nameof(DataRequest),
                entityKey: dataRequest.DataRequestID.ToString(),
                summary: $"Downloaded approved data request export '{fileName}'",
                cancellationToken: cancellationToken);

            return File(
                excelFile,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export DataRequest #{DataRequestId}", id);

            TempData["ErrorMessage"] = User.IsInRole(AppRoles.SystemAdministrator)
                ? $"Export failed: {ex.Message}"
                : "The requested data could not be exported. Please contact the system administrator.";

            return RedirectToAction(
                nameof(MyRequests));
        }
    }
}