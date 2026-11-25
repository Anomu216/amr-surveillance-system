using AMRSurveillance.Models;

namespace AMRSurveillance.Services.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(
        string userId,
        string title,
        string message,
        string notificationType,
        string? link = null,
        CancellationToken cancellationToken = default);

    Task SyncResistanceSpikeNotificationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<List<Notification>> GetUserNotificationsAsync(
        string userId,
        bool unreadOnly = false,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(
        int notificationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default);
}