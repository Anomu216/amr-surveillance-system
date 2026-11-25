using System.Security.Claims;
using System.Text.Json;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class AuditService(
    ApplicationDbContext context,
    IHttpContextAccessor httpContextAccessor) : IAuditService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = false
    };

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityKey = null,
        string? summary = null,
        object? oldValues = null,
        object? newValues = null,
        string? overrideUserId = null,
        string? overrideUserName = null,
        CancellationToken cancellationToken = default)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        var userId = overrideUserId
            ?? user?.FindFirstValue(ClaimTypes.NameIdentifier);

        var userName = overrideUserName
            ?? user?.Identity?.Name
            ?? "System";

        var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString();

        AuditLog entry = new()
        {
            Action = Truncate(action.Trim(), 100)!,
            EntityName = Truncate(entityName.Trim(), 100)!,
            EntityKey = Truncate(entityKey?.Trim(), 150),
            Summary = Truncate(summary?.Trim(), 500),
            OldValuesJson = oldValues is null ? null : JsonSerializer.Serialize(oldValues, s_jsonOptions),
            NewValuesJson = newValues is null ? null : JsonSerializer.Serialize(newValues, s_jsonOptions),
            UserId = Truncate(userId, 450),
            UserName = Truncate(userName, 256),
            IpAddress = Truncate(ipAddress, 64),
            TimestampEat = EastAfricaTime.Now
        };

        context.AuditLogs.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(List<AuditLog> Items, int TotalCount)> SearchAsync(
        DateTime? startDate,
        DateTime? endDate,
        string? entityName,
        string? action,
        string? userQuery,
        string? entityKeyQuery,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = context.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        if (startDate.HasValue)
        {
            var startEat = startDate.Value.Date;
            query = query.Where(x => x.TimestampEat >= startEat);
        }

        if (endDate.HasValue)
        {
            var endExclusiveEat = endDate.Value.Date.AddDays(1);
            query = query.Where(x => x.TimestampEat < endExclusiveEat);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var normalizedEntity = entityName.Trim();
            query = query.Where(x => x.EntityName == normalizedEntity);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalizedAction = action.Trim();
            query = query.Where(x => x.Action == normalizedAction);
        }

        if (!string.IsNullOrWhiteSpace(userQuery))
        {
            var term = userQuery.Trim();
            query = query.Where(x =>
                (x.UserName != null && x.UserName.Contains(term)) ||
                (x.UserId != null && x.UserId.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(entityKeyQuery))
        {
            var keyTerm = entityKeyQuery.Trim();
            query = query.Where(x =>
                (x.EntityKey != null && x.EntityKey.Contains(keyTerm)) ||
                (x.Summary != null && x.Summary.Contains(keyTerm)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var safePage = Math.Max(1, page);
        var safePageSize = Math.Clamp(pageSize, 10, 200);

        var items = await query
            .OrderByDescending(x => x.TimestampEat)
            .ThenByDescending(x => x.AuditLogID)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}