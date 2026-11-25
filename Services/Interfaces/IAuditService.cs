using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityKey = null,
        string? summary = null,
        object? oldValues = null,
        object? newValues = null,
        string? overrideUserId = null,
        string? overrideUserName = null,
        CancellationToken cancellationToken = default);

    Task<(List<AuditLog> Items, int TotalCount)> SearchAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? entityName,
        string? action,
        string? userQuery,
        string? entityKeyQuery,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}