
using AMRSurveillance.Models;
using AMRSurveillance.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace AMRSurveillance.Security;

public static class AdminProvisioner
{
    public static async Task CreateInitialAdminAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        var email = configuration["InitialAdmin:Email"];
        var password = configuration["InitialAdmin:Password"];
        var fullName = configuration["InitialAdmin:FullName"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException(
                "Initial administrator configuration is incomplete.");
        }

        var roleExists = await roleManager.RoleExistsAsync(
            AppRoles.SystemAdministrator);

        if (!roleExists)
        {
            throw new InvalidOperationException(
                "SystemAdministrator role does not exist. Seed roles first.");
        }

        var existingUser = await userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            throw new InvalidOperationException(
                "An account with the configured administrator email already exists. " +
                "No changes were made.");
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            IsActive = true,
            EmailConfirmed = false
        };

        var createResult = await userManager.CreateAsync(
            admin, password);

        if (!createResult.Succeeded)
        {
            var errors = string.Join(
                "; ",
                createResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Initial administrator creation failed: {errors}");
        }

        var roleResult = await userManager.AddToRoleAsync(
            admin, AppRoles.SystemAdministrator);

        if (!roleResult.Succeeded)
        {
            // Remove the newly created account if role assignment fails.
            await userManager.DeleteAsync(admin);

            var errors = string.Join(
                "; ",
                roleResult.Errors.Select(e => e.Description));

            throw new InvalidOperationException(
                $"Administrator role assignment failed: {errors}");
        }
    }
}
