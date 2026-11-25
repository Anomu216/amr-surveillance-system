using System.Security.Cryptography;
using AMRSurveillance.Data;
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using AMRSurveillance.Services.Interfaces;
using AMRSurveillance.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AMRSurveillance.Controllers;

[Authorize(Roles = AppRoles.SystemAdministrator)]
public class UserManagementController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    RoleManager<IdentityRole> roleManager,
    TemporaryPasswordStore temporaryPasswordStore,
    ApplicationDbContext dbContext,
    IAuditService auditService) : Controller
{
    // =========================================================
    // MANAGE USERS DIRECTORY
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? roleFilter,
        string? statusFilter)
    {
        var currentAdmin = await userManager.GetUserAsync(User);
        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        var availableRoles = await GetAvailableRolesAsync();
        var usersQuery = userManager.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            usersQuery = usersQuery.Where(u =>
                (u.FullName != null && u.FullName.Contains(term)) ||
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)));
        }

        if (string.Equals(statusFilter, "Active", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u => u.IsActive);
        }
        else if (string.Equals(statusFilter, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            usersQuery = usersQuery.Where(u => !u.IsActive);
        }
        else if (string.Equals(statusFilter, "Locked", StringComparison.OrdinalIgnoreCase))
        {
            var nowUtc = DateTimeOffset.UtcNow;
            usersQuery = usersQuery.Where(u => u.LockoutEnd != null && u.LockoutEnd > nowUtc);
        }

        var users = await usersQuery
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        var userRolesMap = await (
            from ur in dbContext.UserRoles.AsNoTracking()
            join r in dbContext.Roles.AsNoTracking() on ur.RoleId equals r.Id
            select new { ur.UserId, RoleName = r.Name! }
        ).ToListAsync();

        var rolesByUserId = userRolesMap
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RoleName).OrderBy(n => n).ToList());

        List<UserListItemViewModel> items = [];

        foreach (var user in users)
        {
            var assignedRoles = rolesByUserId.GetValueOrDefault(user.Id) ?? [];

            if (!string.IsNullOrWhiteSpace(roleFilter) &&
                !assignedRoles.Contains(roleFilter, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                IsActive = user.IsActive,
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                MustChangePassword = user.MustChangePassword,
                Roles = [.. assignedRoles.Select(GetRoleDisplayName)]
            });
        }

        ManageUsersIndexViewModel model = new()
        {
            Search = search,
            RoleFilter = roleFilter,
            StatusFilter = statusFilter,
            CurrentAdminId = currentAdmin.Id,
            Users = items,
            AvailableRoles =
            [
                .. availableRoles.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = GetRoleDisplayName(r),
                    Selected = string.Equals(r, roleFilter, StringComparison.OrdinalIgnoreCase)
                })
            ]
        };

        return View(model);
    }

    // =========================================================
    // REASSIGN USER ROLES
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> EditRoles(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var currentAdmin = await userManager.GetUserAsync(User);
        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var availableRoles = await GetAvailableRolesAsync();
        var currentRoles = await userManager.GetRolesAsync(user);

        EditUserRolesViewModel model = new()
        {
            UserId = user.Id,
            FullName = user.FullName ?? string.Empty,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            IsActive = user.IsActive,
            IsCurrentAdmin = user.Id == currentAdmin.Id,
            SelectedRoles = [.. currentRoles],
            AvailableRoles =
            [
                .. availableRoles.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = GetRoleDisplayName(r),
                    Selected = currentRoles.Contains(r)
                })
            ]
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoles(EditUserRolesViewModel model)
    {
        var currentAdmin = await userManager.GetUserAsync(User);
        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        var user = await userManager.FindByIdAsync(model.UserId);
        if (user is null)
        {
            return NotFound();
        }

        var availableRoles = await GetAvailableRolesAsync();
        model.SelectedRoles ??= [];

        var validSelectedRoles = model.SelectedRoles
            .Where(r => availableRoles.Contains(r))
            .Distinct()
            .ToList();

        if (validSelectedRoles.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedRoles),
                "Assign at least one role to the user.");
        }

        // Prevent the logged-in admin from stripping their own SystemAdministrator role
        if (user.Id == currentAdmin.Id &&
            !validSelectedRoles.Contains(AppRoles.SystemAdministrator))
        {
            ModelState.AddModelError(
                nameof(model.SelectedRoles),
                "You cannot remove the System Administrator role from your own account.");
        }

        if (!ModelState.IsValid)
        {
            model.FullName = user.FullName ?? string.Empty;
            model.UserName = user.UserName ?? string.Empty;
            model.Email = user.Email ?? string.Empty;
            model.IsActive = user.IsActive;
            model.IsCurrentAdmin = user.Id == currentAdmin.Id;
            model.AvailableRoles =
            [
                .. availableRoles.Select(r => new SelectListItem
                {
                    Value = r,
                    Text = GetRoleDisplayName(r),
                    Selected = validSelectedRoles.Contains(r)
                })
            ];

            return View(model);
        }

        var previousRoles = (await userManager.GetRolesAsync(user))
            .OrderBy(r => r)
            .ToList();

        var rolesToAdd = validSelectedRoles.Except(previousRoles).ToList();
        var rolesToRemove = previousRoles.Except(validSelectedRoles).ToList();

        if (rolesToAdd.Count == 0 && rolesToRemove.Count == 0)
        {
            TempData["SuccessMessage"] = $"No role changes were needed for {user.UserName}.";
            return RedirectToAction(nameof(Index));
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(removeResult);
                return await EditRoles(user.Id);
            }
        }

        if (rolesToAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(addResult);
                return await EditRoles(user.Id);
            }
        }

        // Refresh security stamp so role claims update promptly on active sessions
        await userManager.UpdateSecurityStampAsync(user);
        await transaction.CommitAsync();

        var updatedRoles = validSelectedRoles.OrderBy(r => r).ToList();

        await auditService.LogAsync(
            action: "RoleChange",
            entityName: nameof(ApplicationUser),
            entityKey: user.UserName ?? user.Id,
            summary: $"Updated roles for {user.UserName}: [{string.Join(", ", updatedRoles)}]",
            oldValues: new { Roles = previousRoles },
            newValues: new { Roles = updatedRoles });

        TempData["SuccessMessage"] = $"Roles updated for {user.FullName ?? user.UserName}.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // ACTIVATE / DEACTIVATE USER
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var currentAdmin = await userManager.GetUserAsync(User);
        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        if (id == currentAdmin.Id)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own administrator account.";
            return RedirectToAction(nameof(Index));
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "The selected user account could not be found.";
            return RedirectToAction(nameof(Index));
        }

        var wasActive = user.IsActive;
        user.IsActive = !wasActive;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            TempData["ErrorMessage"] = "Unable to update user status.";
            return RedirectToAction(nameof(Index));
        }

        // Invalidate active cookies immediately upon deactivation
        await userManager.UpdateSecurityStampAsync(user);

        var statusAction = user.IsActive ? "ActivateUser" : "DeactivateUser";
        await auditService.LogAsync(
            action: statusAction,
            entityName: nameof(ApplicationUser),
            entityKey: user.UserName ?? user.Id,
            summary: $"{(user.IsActive ? "Activated" : "Deactivated")} user account {user.UserName}",
            oldValues: new { IsActive = wasActive },
            newValues: new { IsActive = user.IsActive });

        TempData["SuccessMessage"] =
            $"User {user.FullName ?? user.UserName} has been {(user.IsActive ? "activated" : "deactivated")}.";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // UNLOCK USER ACCOUNT
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "The selected user account could not be found.";
            return RedirectToAction(nameof(Index));
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        await auditService.LogAsync(
            action: "UnlockUser",
            entityName: nameof(ApplicationUser),
            entityKey: user.UserName ?? user.Id,
            summary: $"Unlocked user account {user.UserName}");

        TempData["SuccessMessage"] = $"Account for {user.FullName ?? user.UserName} has been unlocked.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // SAFE DELETE USER
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var currentAdmin = await userManager.GetUserAsync(User);
        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        if (id == currentAdmin.Id)
        {
            TempData["ErrorMessage"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Index));
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            TempData["ErrorMessage"] = "The selected user account could not be found.";
            return RedirectToAction(nameof(Index));
        }

        // Prevent deletion if the user has historical records or audit trail activity
        var hasDataRequests = await dbContext.DataRequests
            .AsNoTracking()
            .AnyAsync(r => r.RequestedByUserID == user.Id || r.ReviewedByUserID == user.Id);

        var hasAuditHistory = await dbContext.AuditLogs
            .AsNoTracking()
            .AnyAsync(a => a.UserId == user.Id);

        if (hasDataRequests || hasAuditHistory)
        {
            TempData["ErrorMessage"] =
                $"Cannot permanently delete {user.UserName} because they have historical activity or data requests in the system. Deactivate the account instead to preserve audit integrity.";
            return RedirectToAction(nameof(Index));
        }

        var snapshot = new
        {
            user.Id,
            user.FullName,
            user.UserName,
            user.Email,
            Roles = await userManager.GetRolesAsync(user)
        };

        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", deleteResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await auditService.LogAsync(
            action: "DeleteUser",
            entityName: nameof(ApplicationUser),
            entityKey: snapshot.UserName ?? snapshot.Id,
            summary: $"Deleted unused user account {snapshot.UserName}",
            oldValues: snapshot);

        TempData["SuccessMessage"] = $"User account {snapshot.UserName} was permanently deleted.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // CREATE USER
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> CreateUser()
    {
        var model = new CreateUserViewModel();

        await PopulateRolesAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(
        CreateUserViewModel model)
    {
        var availableRoles = await GetAvailableRolesAsync();

        model.SelectedRoles ??= [];

        if (model.SelectedRoles.Count == 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedRoles),
                "Assign at least one role to the new user.");
        }

        var invalidRoles = model.SelectedRoles
            .Where(role => !availableRoles.Contains(role))
            .Distinct()
            .ToList();

        if (invalidRoles.Count > 0)
        {
            ModelState.AddModelError(
                nameof(model.SelectedRoles),
                "One or more selected roles are invalid.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync(model);
            return View(model);
        }

        var currentAdmin = await userManager.GetUserAsync(User);

        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        var fullName = model.FullName.Trim();
        var username = model.Username.Trim();
        var email = model.Email.Trim();

        if (await userManager.FindByNameAsync(username) is not null)
        {
            ModelState.AddModelError(
                nameof(model.Username),
                "This username is already in use.");
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError(
                nameof(model.Email),
                "This email address is already associated with an account.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateRolesAsync(model);
            return View(model);
        }

        var temporaryPassword = GenerateTemporaryPassword();

        var newUser = new ApplicationUser
        {
            FullName = fullName,
            UserName = username,
            Email = email,
            EmailConfirmed = false,
            IsActive = true,
            MustChangePassword = true
        };

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var createResult = await userManager.CreateAsync(
            newUser,
            temporaryPassword);

        if (!createResult.Succeeded)
        {
            await transaction.RollbackAsync();

            AddIdentityErrors(createResult);

            await PopulateRolesAsync(model);
            return View(model);
        }

        var assignedRoles = model.SelectedRoles.Distinct().ToList();
        var roleResult = await userManager.AddToRolesAsync(
            newUser,
            assignedRoles);

        if (!roleResult.Succeeded)
        {
            await transaction.RollbackAsync();

            AddIdentityErrors(roleResult);

            await PopulateRolesAsync(model);
            return View(model);
        }

        await transaction.CommitAsync();

        await auditService.LogAsync(
            action: "CreateUser",
            entityName: nameof(ApplicationUser),
            entityKey: newUser.UserName,
            summary: $"Created user {newUser.UserName} with roles [{string.Join(", ", assignedRoles)}]",
            newValues: new
            {
                newUser.FullName,
                newUser.UserName,
                newUser.Email,
                Roles = assignedRoles
            });

        var reference = temporaryPasswordStore.Store(
            newUser.UserName ?? newUser.Email ?? "User",
            temporaryPassword);

        return RedirectToAction(
            nameof(CreateUserConfirmation),
            new { reference });
    }

    [HttpGet]
    public IActionResult CreateUserConfirmation(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !temporaryPasswordStore.TryTake(
                reference,
                out var userName,
                out var temporaryPassword))
        {
            return View("ResetPasswordResultUnavailable");
        }

        ViewBag.CreatedUserName = userName;
        ViewBag.TemporaryPassword = temporaryPassword;

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";

        return View();
    }

    // =========================================================
    // RESET USER PASSWORD
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? userId = null)
    {
        await PopulateUsersAsync(userId);

        return View(new AdminPasswordResetViewModel
        {
            UserId = userId ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        AdminPasswordResetViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        var currentAdmin = await userManager.GetUserAsync(User);

        if (currentAdmin is null || !currentAdmin.IsActive)
        {
            await signInManager.SignOutAsync();
            return Challenge();
        }

        if (model.UserId == currentAdmin.Id)
        {
            ModelState.AddModelError(
                string.Empty,
                "You cannot reset your own password from this screen.");

            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.UserId);

        if (user is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "The selected user account could not be found.");

            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(
                string.Empty,
                "The selected user account is inactive.");

            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        var temporaryPassword = GenerateTemporaryPassword();

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync();

        var resetToken =
            await userManager.GeneratePasswordResetTokenAsync(user);

        var resetResult = await userManager.ResetPasswordAsync(
            user,
            resetToken,
            temporaryPassword);

        if (!resetResult.Succeeded)
        {
            await transaction.RollbackAsync();

            AddIdentityErrors(resetResult);

            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        user.MustChangePassword = true;

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync();

            AddIdentityErrors(updateResult);

            await PopulateUsersAsync(model.UserId);
            return View(model);
        }

        await transaction.CommitAsync();

        await auditService.LogAsync(
            action: "AdminPasswordReset",
            entityName: nameof(ApplicationUser),
            entityKey: user.UserName ?? user.Id,
            summary: $"Administrator reset password for {user.UserName}");

        var reference = temporaryPasswordStore.Store(
            user.FullName ?? user.UserName ?? user.Email ?? "User",
            temporaryPassword);

        return RedirectToAction(
            nameof(ResetPasswordConfirmation),
            new { reference });
    }

    [HttpGet]
    public IActionResult ResetPasswordConfirmation(
        string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !temporaryPasswordStore.TryTake(
                reference,
                out var userName,
                out var temporaryPassword))
        {
            return View("ResetPasswordResultUnavailable");
        }

        ViewBag.ResetUserName = userName;
        ViewBag.TemporaryPassword = temporaryPassword;

        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers.Pragma = "no-cache";

        return View();
    }

    // =========================================================
    // PRIVATE HELPERS
    // =========================================================

    private async Task PopulateRolesAsync(
        CreateUserViewModel model)
    {
        var roles = await GetAvailableRolesAsync();

        ViewBag.AvailableRoles = roles
            .Select(role => new SelectListItem
            {
                Value = role,
                Text = GetRoleDisplayName(role),
                Selected = model.SelectedRoles.Contains(role)
            })
            .ToList();
    }

    private async Task<List<string>> GetAvailableRolesAsync()
    {
        string[] configuredRoles =
        [
            AppRoles.SystemAdministrator,
            AppRoles.Analyst,
            AppRoles.GenomicsStaff,
            AppRoles.DataEntryStaff,
            AppRoles.DataExportApprover
        ];

        var existingRoles = await roleManager.Roles
            .Select(role => role.Name!)
            .ToListAsync();

        return [.. configuredRoles.Where(role => existingRoles.Contains(role))];
    }

    private static string GetRoleDisplayName(string role)
    {
        return role switch
        {
            AppRoles.SystemAdministrator => "System Administrator",
            AppRoles.Analyst => "Analyst",
            AppRoles.GenomicsStaff => "Genomics Staff",
            AppRoles.DataEntryStaff => "Data Entry Staff",
            AppRoles.DataExportApprover => "Data Export Approver",
            _ => role
        };
    }

    private async Task PopulateUsersAsync(string? selectedUserId = null)
    {
        var currentAdmin = await userManager.GetUserAsync(User);

        var users = await userManager.Users
            .Where(u =>
                u.IsActive &&
                (currentAdmin == null || u.Id != currentAdmin.Id))
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Email)
            .ToListAsync();

        ViewBag.Users = users.Select(u => new SelectListItem
        {
            Value = u.Id,
            Text = !string.IsNullOrWhiteSpace(u.FullName)
                ? $"{u.FullName} ({u.Email ?? u.UserName})"
                : u.Email ?? u.UserName ?? u.Id,
            Selected = !string.IsNullOrWhiteSpace(selectedUserId) && u.Id == selectedUserId
        }).ToList();
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(
                string.Empty,
                error.Description);
        }
    }

    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&*?";

        List<char> characters =
        [
            GetRandomCharacter(upper),
            GetRandomCharacter(upper),
            GetRandomCharacter(lower),
            GetRandomCharacter(lower),
            GetRandomCharacter(digits),
            GetRandomCharacter(digits),
            GetRandomCharacter(special),
            GetRandomCharacter(special)
        ];

        const string all = upper + lower + digits + special;

        while (characters.Count < 16)
        {
            characters.Add(GetRandomCharacter(all));
        }

        for (var i = characters.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (characters[i], characters[j]) = (characters[j], characters[i]);
        }

        return new string([.. characters]);
    }

    private static char GetRandomCharacter(string characters)
    {
        return characters[RandomNumberGenerator.GetInt32(characters.Length)];
    }
}