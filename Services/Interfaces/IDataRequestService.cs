using AMRSurveillance.Models;
using AMRSurveillance.ViewModels.DataRequests;

namespace AMRSurveillance.Services.Interfaces;

public interface IDataRequestService
{
    Task<DataRequestViewModel>
        GetDataRequestViewModelAsync(
            CancellationToken cancellationToken = default);

    Task<List<DataRequestIdentifierValidationViewModel>>
        ValidateIdentifierFileAsync(
            string identifierType,
            IFormFile identifierFile,
            CancellationToken cancellationToken = default);

    Task<int>
        CreateDataRequestAsync(
            DataRequestViewModel model,
            string requestedByUserID,
            CancellationToken cancellationToken = default);

    Task<List<DataRequest>> GetUserRequestsAsync(
    string userId,
    CancellationToken cancellationToken = default);

    // ============================================================
    // APPROVAL QUEUE
    // ============================================================

    Task<List<DataRequest>> GetPendingRequestsAsync(
        CancellationToken cancellationToken = default);

    Task<DataRequest?> GetDataRequestByIdAsync(
        int dataRequestId,
        CancellationToken cancellationToken = default);

    Task ApproveDataRequestAsync(
        int dataRequestId,
        string reviewedByUserId,
        string? reviewComments,
        CancellationToken cancellationToken = default);

    Task RejectDataRequestAsync(
        int dataRequestId,
        string reviewedByUserId,
        string reviewComments,
        CancellationToken cancellationToken = default);

    // ============================================================
    // EXPORT
    // ============================================================

    Task<byte[]> GenerateExcelExportAsync(
        DataRequest dataRequest,
        CancellationToken cancellationToken = default);

    Task MarkRequestAsExportedAsync(
        int dataRequestId,
        CancellationToken cancellationToken = default);
}