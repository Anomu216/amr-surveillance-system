using System.Security.Claims;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.ViewComponents;

public class NotificationBellViewComponent(
    INotificationService notificationService)
    : ViewComponent
{
    private readonly INotificationService _notificationService = notificationService;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return View("~/Views/Shared/Components/Default.cshtml",
                new NotificationBellViewModel());
        }

        await _notificationService.SyncResistanceSpikeNotificationsAsync(
            userId,
            HttpContext.RequestAborted);

        var notifications = await _notificationService
            .GetUserNotificationsAsync(
                userId,
                cancellationToken: HttpContext.RequestAborted);

        var unreadCount = await _notificationService
            .GetUnreadCountAsync(
                userId,
                HttpContext.RequestAborted);

        var model = new NotificationBellViewModel
        {
            Notifications = notifications,
            UnreadCount = unreadCount
        };

        return View("~/Views/Shared/Components/Default.cshtml", model);
    }
}