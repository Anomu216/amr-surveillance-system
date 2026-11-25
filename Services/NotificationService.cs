using System.Collections.Concurrent;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Services;

public class NotificationService(
    ApplicationDbContext context,
    IDashboardService dashboardService,
    IReportService reportService) : INotificationService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IDashboardService _dashboardService = dashboardService;
    private readonly IReportService _reportService = reportService;

    private const string SpikeNotificationType = "ResistanceSpike";
    private const string MonthlyRollupSpikeNotificationType = "MonthlyRollupSpike";
    private static readonly TimeSpan SpikeEvaluationCooldown = TimeSpan.FromHours(6);
    private static readonly TimeSpan SpikeDeduplicationWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan QueryTimeoutGuard = TimeSpan.FromSeconds(25);
    private static readonly ConcurrentDictionary<string, DateTime> LastSpikeSyncByUser =
        new(StringComparer.OrdinalIgnoreCase);

    // ============================================================
    // CREATE NOTIFICATION
    // ============================================================

    public async Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        string notificationType,
        string? link = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException(
                "A user ID is required.",
                nameof(userId));
        }

        using var cts = CreateScopedTokenSource(cancellationToken);

        Notification notification = new()
        {
            UserID = userId,
            Title = title,
            Message = message,
            NotificationType = notificationType,
            Link = link,
            IsRead = false,
            CreatedAt = EastAfricaTime.Now
        };

        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cts.Token);
    }

    // ============================================================
    // SYNC RESISTANCE SPIKE NOTIFICATIONS (DASHBOARD + REPORTS)
    // ============================================================

    public async Task SyncResistanceSpikeNotificationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var now = EastAfricaTime.Now;

        if (LastSpikeSyncByUser.TryGetValue(userId, out var lastChecked) &&
            now - lastChecked < SpikeEvaluationCooldown)
        {
            return;
        }

        try
        {
            using var cts = CreateScopedTokenSource(cancellationToken);

            var rollingSpikes = await _dashboardService.DetectSystemWideRollingSpikesAsync(
                cts.Token);

            var monthlyRollupSpikes = await _reportService.DetectMonthlyRollupSpikesAsync(
                cts.Token);

            LastSpikeSyncByUser[userId] = now;

            if (rollingSpikes.Count == 0 && monthlyRollupSpikes.Count == 0)
            {
                return;
            }

            var cutoff = now.Subtract(SpikeDeduplicationWindow);

            var existingTitles = await _context.Notifications
                .AsNoTracking()
                .Where(n =>
                    n.UserID == userId &&
                    (n.NotificationType == SpikeNotificationType ||
                     n.NotificationType == MonthlyRollupSpikeNotificationType) &&
                    n.CreatedAt >= cutoff)
                .Select(n => n.Title)
                .ToListAsync(cts.Token);

            HashSet<string> existingTitleSet = new(
                existingTitles,
                StringComparer.OrdinalIgnoreCase);

            List<Notification> newNotifications = [];

            foreach (var spike in rollingSpikes)
            {
                var title = BuildSpikeTitle(spike);
                if (existingTitleSet.Contains(title))
                {
                    continue;
                }

                existingTitleSet.Add(title);

                newNotifications.Add(new()
                {
                    UserID = userId,
                    Title = title.Length > 200 ? title[..200] : title,
                    Message = spike.SummaryMessage.Length > 2000
                        ? spike.SummaryMessage[..2000]
                        : spike.SummaryMessage,
                    NotificationType = SpikeNotificationType,
                    Link = BuildSpikeDashboardLink(spike),
                    IsRead = false,
                    CreatedAt = now
                });
            }

            foreach (var rollupSpike in monthlyRollupSpikes)
            {
                var title = BuildMonthlyRollupSpikeTitle(rollupSpike);
                if (existingTitleSet.Contains(title))
                {
                    continue;
                }

                existingTitleSet.Add(title);

                newNotifications.Add(new()
                {
                    UserID = userId,
                    Title = title.Length > 200 ? title[..200] : title,
                    Message = rollupSpike.SummaryMessage.Length > 2000
                        ? rollupSpike.SummaryMessage[..2000]
                        : rollupSpike.SummaryMessage,
                    NotificationType = MonthlyRollupSpikeNotificationType,
                    Link = BuildMonthlyRollupReportLink(rollupSpike),
                    IsRead = false,
                    CreatedAt = now
                });
            }

            if (newNotifications.Count > 0)
            {
                _context.Notifications.AddRange(newNotifications);
                await _context.SaveChangesAsync(cts.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out during spike sync; avoid crashing the page render
        }
    }

    private static string BuildSpikeTitle(ResistanceSpikeAlert spike)
    {
        return spike.MetricType switch
        {
            "MDR" => $"Resistance Spike: MDR ({spike.OrganismLabel})",
            "MRSA" => "Resistance Spike: MRSA (Staphylococcus aureus)",
            _ => $"Resistance Spike: {spike.OrganismLabel} — {spike.MetricName}"
        };
    }

    private static string BuildSpikeDashboardLink(ResistanceSpikeAlert spike)
    {
        if (string.Equals(spike.MetricType, "MDR", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(spike.OrganismLabel, "All Accepted Isolates", StringComparison.OrdinalIgnoreCase))
        {
            return "/";
        }

        var encodedOrganism = Uri.EscapeDataString(spike.OrganismLabel);
        return $"/?ASTOrganismIDs={encodedOrganism}";
    }

    private static string BuildMonthlyRollupSpikeTitle(MonthlyRollupSpikeAlert spike)
    {
        return $"Monthly Rollup Spike: {spike.OrganismLabel} — {spike.MetricName}";
    }

    private static string BuildMonthlyRollupReportLink(MonthlyRollupSpikeAlert spike)
    {
        if (string.Equals(spike.MetricType, "ReportAntibiotic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(spike.MetricType, "MRSA", StringComparison.OrdinalIgnoreCase))
        {
            var encodedOrganism = Uri.EscapeDataString(spike.OrganismLabel);
            return $"/Report?organism={encodedOrganism}";
        }

        return "/Report";
    }

    // ============================================================
    // GET USER NOTIFICATIONS
    // ============================================================

    public async Task<List<Notification>>
        GetUserNotificationsAsync(
            string userId,
            bool unreadOnly = false,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || cancellationToken.IsCancellationRequested)
        {
            return [];
        }

        using var cts = CreateScopedTokenSource(cancellationToken);

        IQueryable<Notification> query =
            _context.Notifications
                .AsNoTracking()
                .Where(x => x.UserID == userId);

        if (unreadOnly)
        {
            query = query.Where(x => !x.IsRead);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cts.Token);
    }

    // ============================================================
    // GET UNREAD COUNT
    // ============================================================

    public async Task<int>
        GetUnreadCountAsync(
            string userId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || cancellationToken.IsCancellationRequested)
        {
            return 0;
        }

        using var cts = CreateScopedTokenSource(cancellationToken);

        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(
                x => x.UserID == userId && !x.IsRead,
                cts.Token);
    }

    // ============================================================
    // MARK ONE NOTIFICATION AS READ
    // ============================================================

    public async Task MarkAsReadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        using var cts = CreateScopedTokenSource(cancellationToken);

        var notification =
            await _context.Notifications
                .FirstOrDefaultAsync(
                    x => x.NotificationID == notificationId && x.UserID == userId,
                    cts.Token)
            ?? throw new InvalidOperationException(
                "Notification was not found.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = EastAfricaTime.Now;

            await _context.SaveChangesAsync(cts.Token);
        }
    }

    // ============================================================
    // MARK ALL NOTIFICATIONS AS READ
    // ============================================================

    public async Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        using var cts = CreateScopedTokenSource(cancellationToken);

        var notifications =
            await _context.Notifications
                .Where(x => x.UserID == userId && !x.IsRead)
                .ToListAsync(cts.Token);

        if (notifications.Count == 0)
        {
            return;
        }

        var readAt = EastAfricaTime.Now;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
        }

        await _context.SaveChangesAsync(cts.Token);
    }

    // ============================================================
    // CANCELLATION HELPER
    // ============================================================

    private static CancellationTokenSource CreateScopedTokenSource(CancellationToken externalToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        cts.CancelAfter(QueryTimeoutGuard);
        return cts;
    }
}