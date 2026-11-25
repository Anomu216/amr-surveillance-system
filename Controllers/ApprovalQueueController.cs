using System.Security.Claims;
using System.Text.Json;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels.DataRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers;

[Authorize(Roles =
    AppRoles.SystemAdministrator + "," +
    AppRoles.DataExportApprover)]
public class ApprovalQueueController(
    IDataRequestService dataRequestService,
    UserManager<ApplicationUser> userManager) : Controller
{
    private readonly IDataRequestService _dataRequestService = dataRequestService;
    private readonly UserManager<ApplicationUser> _userManager = userManager;


    // ============================================================
    // APPROVAL QUEUE
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var requests =
            await _dataRequestService
                .GetPendingRequestsAsync(
                    cancellationToken);

        var userIds =
            requests
                .Select(x => x.RequestedByUserID)
                .Distinct()
                .ToList();

        var users =
            await _userManager.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

        var requesterNames =
            users.ToDictionary(
                x => x.Id,
                x => !string.IsNullOrWhiteSpace(x.FullName)
                    ? x.FullName
                    : x.UserName ?? "Unknown User");

        ViewBag.RequesterNames =
            requesterNames;

        return View(requests);
    }


    // ============================================================
    // REVIEW REQUEST
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Review(
        int id,
        CancellationToken cancellationToken)
    {
        var request =
            await _dataRequestService
                .GetDataRequestByIdAsync(
                    id,
                    cancellationToken);

        if (request is null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isOwnRequest = string.Equals(
            request.RequestedByUserID,
            currentUserId,
            StringComparison.Ordinal);

        ViewBag.IsSelfApprovalBlocked =
            isOwnRequest && !User.IsInRole(AppRoles.SystemAdministrator);


        // ========================================================
        // GET REQUESTER DETAILS
        // ========================================================

        var user =
            await _userManager.FindByIdAsync(
                request.RequestedByUserID);

        var requesterName =
            user?.FullName
            ?? user?.UserName
            ?? "Unknown User";


        // ========================================================
        // CREATE REVIEW VIEW MODEL
        // ========================================================

        var model =
            new DataRequestReviewViewModel
            {
                DataRequestID =
                    request.DataRequestID,

                RequesterName =
                    requesterName,

                Reason =
                    request.Reason,

                RequestedAt =
                    request.RequestedAt,

                Status =
                    request.Status,

                IdentifierType =
                    request.IdentifierType,

                ReviewComments =
                    request.ReviewComments
            };


        // ========================================================
        // DESERIALIZE SELECTED DATA
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
                request.SelectedData))
        {
            try
            {
                var selectedData =
                    JsonSerializer.Deserialize<
                        List<SelectedDatasetJsonModel>>(
                        request.SelectedData);

                if (selectedData is not null)
                {
                    var catalogue =
                        DataExportCatalogue.GetDatasets();

                    foreach (var selectedDataset
                        in selectedData)
                    {
                        var catalogueDataset =
                            catalogue.FirstOrDefault(
                                x => string.Equals(
                                    x.Value,
                                    selectedDataset.Dataset,
                                    StringComparison.OrdinalIgnoreCase));

                        if (catalogueDataset is null)
                        {
                            continue;
                        }

                        var reviewDataset =
                            new DataRequestDatasetViewModel
                            {
                                Name =
                                    catalogueDataset.Name,

                                Value =
                                    catalogueDataset.Value,

                                Columns = []
                            };


                        foreach (var selectedColumn
                            in selectedDataset.Columns)
                        {
                            var catalogueColumn =
                                catalogueDataset.Columns
                                    .FirstOrDefault(
                                        x => string.Equals(
                                            x.Value,
                                            selectedColumn.Value,
                                            StringComparison.OrdinalIgnoreCase));

                            if (catalogueColumn is not null)
                            {
                                reviewDataset.Columns.Add(
                                    new DataRequestColumnViewModel
                                    {
                                        Value =
                                            catalogueColumn.Value,

                                        Text =
                                            catalogueColumn.Text
                                    });
                            }
                            else
                            {
                                reviewDataset.Columns.Add(
                                    new DataRequestColumnViewModel
                                    {
                                        Value =
                                            selectedColumn.Value,

                                        Text =
                                            selectedColumn.Text
                                    });
                            }
                        }

                        model.SelectedDatasets.Add(
                            reviewDataset);
                    }
                }
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "The selected data for this request could not be read.";
            }
        }


        // ========================================================
        // DESERIALIZE IDENTIFIER VALUES
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
                request.IdentifierValues))
        {
            try
            {
                var identifiers =
                    JsonSerializer.Deserialize<List<string>>(
                        request.IdentifierValues);

                if (identifiers is not null)
                {
                    model.IdentifierValues =
                        identifiers;
                }
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "The identifiers for this request could not be read.";
            }
        }


        // ========================================================
        // DESERIALIZE FILTERS
        // ========================================================

        if (!string.IsNullOrWhiteSpace(
                request.Filters))
        {
            try
            {
                var filters =
                    JsonSerializer.Deserialize<
                        DataRequestFiltersJsonModel>(
                        request.Filters);

                if (filters is not null)
                {
                    model.StartDate =
                        filters.StartDate;

                    model.EndDate =
                        filters.EndDate;

                    model.FacilityIDs =
                        filters.FacilityIDs ?? [];

                    model.OrganismIDs =
                        filters.OrganismIDs ?? [];
                }
            }
            catch (JsonException)
            {
                TempData["ErrorMessage"] =
                    "The filters for this request could not be read.";
            }
        }


        return View(model);
    }


    // ============================================================
    // APPROVE REQUEST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(
        int id,
        string? reviewComments,
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            await _dataRequestService
                .ApproveDataRequestAsync(
                    id,
                    userId,
                    reviewComments,
                    cancellationToken);

            TempData["SuccessMessage"] =
                $"Data request #{id} has been approved.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;

            return RedirectToAction(
                nameof(Review),
                new { id });
        }

        return RedirectToAction(
            nameof(Index));
    }


    // ============================================================
    // REJECT REQUEST
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(
        int id,
        string reviewComments,
        CancellationToken cancellationToken)
    {
        var userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            await _dataRequestService
                .RejectDataRequestAsync(
                    id,
                    userId,
                    reviewComments,
                    cancellationToken);

            TempData["SuccessMessage"] =
                $"Data request #{id} has been rejected.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] =
                ex.Message;

            return RedirectToAction(
                nameof(Review),
                new { id });
        }

        return RedirectToAction(
            nameof(Index));
    }
}


// ============================================================
// JSON HELPER MODELS
// ============================================================

internal sealed class SelectedDatasetJsonModel
{
    public string Dataset { get; set; } =
        string.Empty;

    public List<SelectedColumnJsonModel> Columns { get; set; } = [];
}


internal sealed class SelectedColumnJsonModel
{
    public string Value { get; set; } =
        string.Empty;

    public string Text { get; set; } =
        string.Empty;
}


internal sealed class DataRequestFiltersJsonModel
{
    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public List<string>? FacilityIDs { get; set; }

    public List<string>? OrganismIDs { get; set; }
}