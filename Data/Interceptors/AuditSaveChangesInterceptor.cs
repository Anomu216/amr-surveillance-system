using System.Security.Claims;
using System.Text.Json;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AMRSurveillance.Data.Interceptors;

public sealed class AuditSaveChangesInterceptor(
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly HashSet<string> s_excludedEntities = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AuditLog),
        "Notification",
        "UserNotification"
    };

    private static readonly HashSet<string> s_sensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "TwoFactorEnabled"
    };

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            AppendAuditLogs(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            AppendAuditLogs(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditLogs(DbContext context)
    {
        context.ChangeTracker.DetectChanges();

        var httpContext = httpContextAccessor.HttpContext;
        var userId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = httpContext?.User?.Identity?.Name ?? "System";
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var nowEat = EastAfricaTime.Now;

        List<AuditLog> pendingLogs = [];

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var entityName = entry.Metadata.ClrType.Name;
            if (s_excludedEntities.Contains(entityName))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => null
            };

            if (action is null)
            {
                continue;
            }

            var entityKey = ResolvePrimaryKey(entry);
            var (oldValuesJson, newValuesJson, changedColumns) = ExtractDiffs(entry);

            // Skip no-op updates where no tracked non-sensitive properties actually changed
            if (entry.State == EntityState.Modified && changedColumns.Count == 0)
            {
                continue;
            }

            var summary = entry.State switch
            {
                EntityState.Added => $"Created {entityName} ({entityKey})",
                EntityState.Modified => $"Updated {entityName} ({entityKey}): {string.Join(", ", changedColumns)}",
                EntityState.Deleted => $"Deleted {entityName} ({entityKey})",
                _ => $"{action} {entityName}"
            };

            pendingLogs.Add(new AuditLog
            {
                Action = Truncate(action, 100)!,
                EntityName = Truncate(entityName, 100)!,
                EntityKey = Truncate(entityKey, 150),
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                Summary = Truncate(summary, 500),
                UserId = Truncate(userId, 450),
                UserName = Truncate(userName, 256),
                IpAddress = Truncate(ipAddress, 64),
                TimestampEat = nowEat
            });
        }

        if (pendingLogs.Count > 0)
        {
            context.Set<AuditLog>().AddRange(pendingLogs);
        }
    }

    private static string ResolvePrimaryKey(EntityEntry entry)
    {
        var keyParts = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p =>
            {
                var val = entry.State == EntityState.Deleted
                    ? p.OriginalValue
                    : p.CurrentValue;
                return val?.ToString() ?? "Pending";
            })
            .ToList();

        return keyParts.Count > 0 ? string.Join(":", keyParts) : "N/A";
    }

    private static (string? OldValuesJson, string? NewValuesJson, List<string> ChangedColumns) ExtractDiffs(EntityEntry entry)
    {
        Dictionary<string, object?> oldDict = [];
        Dictionary<string, object?> newDict = [];
        List<string> changedColumns = [];

        foreach (var prop in entry.Properties)
        {
            var propName = prop.Metadata.Name;
            if (s_sensitiveProperties.Contains(propName))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    newDict[propName] = prop.CurrentValue;
                    break;

                case EntityState.Deleted:
                    oldDict[propName] = prop.OriginalValue;
                    break;

                case EntityState.Modified:
                    if (prop.IsModified && !Equals(prop.OriginalValue, prop.CurrentValue))
                    {
                        oldDict[propName] = prop.OriginalValue;
                        newDict[propName] = prop.CurrentValue;
                        changedColumns.Add(propName);
                    }
                    break;
            }
        }

        var oldJson = oldDict.Count > 0 ? JsonSerializer.Serialize(oldDict) : null;
        var newJson = newDict.Count > 0 ? JsonSerializer.Serialize(newDict) : null;

        return (oldJson, newJson, changedColumns);
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