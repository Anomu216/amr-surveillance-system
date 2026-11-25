
using AMRSurveillance.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace AMRSurveillance.Security;

public class ActiveUserFilter : IAsyncActionFilter
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ActiveUserFilter(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var userId = user.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var applicationUser = userId is null
            ? null
            : await _userManager.FindByIdAsync(userId);

        if (applicationUser is null || !applicationUser.IsActive)
        {
            context.Result = new RedirectToActionResult(
                "Login",
                "Account",
                null);

            return;
        }

        // Allow users who must change their password to access
        // only the password-change page and sign-out action.
        if (applicationUser.MustChangePassword)
        {
            var controller = context.RouteData.Values["controller"]
                ?.ToString();

            var action = context.RouteData.Values["action"]
                ?.ToString();

            var isAllowedAction =
                string.Equals(
                    controller,
                    "Account",
                    StringComparison.OrdinalIgnoreCase)
                &&
                (
                    string.Equals(
                        action,
                        "ChangePassword",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    string.Equals(
                        action,
                        "Logout",
                        StringComparison.OrdinalIgnoreCase)
                );

            if (!isAllowedAction)
            {
                context.Result = new RedirectToActionResult(
                    "ChangePassword",
                    "Account",
                    null);

                return;
            }
        }

        await next();
    }
}
