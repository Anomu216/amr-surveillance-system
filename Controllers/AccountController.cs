using System.Security.Claims;
using AMRSurveillance.Models;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AMRSurveillance.Controllers;

[Authorize]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IAuditService auditService) : Controller
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IAuditService _auditService = auditService;

    // =========================================================
    // LOGIN - GET
    // =========================================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(GetSafeReturnUrl(returnUrl));
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    // =========================================================
    // LOGIN - POST
    // =========================================================

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Username.Trim();
        var user = await _userManager.FindByNameAsync(identifier)
                   ?? await _userManager.FindByEmailAsync(identifier);

        if (user is null)
        {
            await _auditService.LogAsync(
                action: "LoginFailed",
                entityName: nameof(ApplicationUser),
                entityKey: identifier,
                summary: $"Failed login attempt for unknown identifier '{identifier}'",
                cancellationToken: cancellationToken);

            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            await _auditService.LogAsync(
                action: "LoginBlocked",
                entityName: nameof(ApplicationUser),
                entityKey: user.Id,
                summary: $"Blocked login attempt for inactive account '{user.UserName}'",
                cancellationToken: cancellationToken);

            ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact the system administrator.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditService.LogAsync(
                action: "Login",
                entityName: nameof(ApplicationUser),
                entityKey: user.Id,
                summary: $"User '{user.UserName}' signed in successfully",
                overrideUserId: user.Id,
                overrideUserName: user.UserName,
                cancellationToken: cancellationToken);

            return LocalRedirect(GetSafeReturnUrl(returnUrl));
        }

        if (result.IsLockedOut)
        {
            await _auditService.LogAsync(
                action: "AccountLocked",
                entityName: nameof(ApplicationUser),
                entityKey: user.Id,
                summary: $"Account '{user.UserName}' locked out due to multiple failed login attempts",
                cancellationToken: cancellationToken);

            ModelState.AddModelError(string.Empty, "This account has been temporarily locked due to multiple failed login attempts.");
            return View(model);
        }

        await _auditService.LogAsync(
            action: "LoginFailed",
            entityName: nameof(ApplicationUser),
            entityKey: user.Id,
            summary: $"Failed password attempt for user '{user.UserName}'",
            cancellationToken: cancellationToken);

        ModelState.AddModelError(string.Empty, "Invalid username or password.");
        return View(model);
    }

    // =========================================================
    // CHANGE PASSWORD - GET
    // =========================================================

    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    // =========================================================
    // CHANGE PASSWORD - POST
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var changeResult = await _userManager.ChangePasswordAsync(
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!changeResult.Succeeded)
        {
            foreach (var error in changeResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);

        await _auditService.LogAsync(
            action: "PasswordChanged",
            entityName: nameof(ApplicationUser),
            entityKey: user.Id,
            summary: $"User '{user.UserName}' changed their own password",
            cancellationToken: cancellationToken);

        TempData["SuccessMessage"] = "Your password has been updated successfully.";

        return RedirectToAction(nameof(ChangePassword));
    }

    // =========================================================
    // LOGOUT
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name ?? "Unknown";

        await _signInManager.SignOutAsync();

        await _auditService.LogAsync(
            action: "Logout",
            entityName: nameof(ApplicationUser),
            entityKey: userId,
            summary: $"User '{userName}' signed out",
            cancellationToken: cancellationToken);

        return RedirectToAction(nameof(Login));
    }

    // =========================================================
    // ACCESS DENIED
    // =========================================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // =========================================================
    // HELPER
    // =========================================================

    private string GetSafeReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return returnUrl;
        }

        return Url.Action("Index", "Home") ?? "/";
    }
}