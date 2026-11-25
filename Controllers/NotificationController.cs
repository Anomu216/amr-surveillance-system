using System.Security.Claims;
using AMRSurveillance.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

[Authorize]
public class NotificationController(
    INotificationService notificationService) : Controller
{
    private readonly INotificationService _notificationService = notificationService;

    // ============================================================
    // OPEN NOTIFICATION
    //
    // Marks the notification as read, then redirects the user
    // to its associated destination.
    // ============================================================

    [HttpGet]
    public async Task<IActionResult> Open(
        int id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        try
        {
            await _notificationService
                .MarkAsReadAsync(
                    id,
                    userId,
                    cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        // Only allow local URLs for security.
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(
            "Index",
            "Home");
    }

    // ============================================================
    // MARK ALL NOTIFICATIONS AS READ
    // ============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await _notificationService
            .MarkAllAsReadAsync(
                userId,
                cancellationToken);

        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(
            "Index",
            "Home");
    }
}