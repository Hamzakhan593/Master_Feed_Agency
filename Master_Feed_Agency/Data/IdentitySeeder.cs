using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Identity;

namespace Master_Feed_Agency.Data;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, IConfiguration configuration, ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var permissionService = scope.ServiceProvider.GetRequiredService<UserPermissionService>();

        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Could not create role '{roleName}': {string.Join(", ", result.Errors.Select(x => x.Description))}");
                }
            }
        }

        var ownerEmail = configuration["SeedOwner:Email"]?.Trim();
        var ownerPassword = configuration["SeedOwner:Password"];
        var ownerFullName = configuration["SeedOwner:FullName"]?.Trim();

        if (!string.IsNullOrWhiteSpace(ownerEmail) && !string.IsNullOrWhiteSpace(ownerPassword))
        {
            var owner = await userManager.FindByEmailAsync(ownerEmail);
            var createdNow = false;

            if (owner is null)
            {
                owner = new ApplicationUser
                {
                    UserName = ownerEmail,
                    Email = ownerEmail,
                    EmailConfirmed = true,
                    FullName = string.IsNullOrWhiteSpace(ownerFullName) ? "Owner" : ownerFullName,
                    IsActive = true,
                    MustChangePassword = false,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(owner, ownerPassword);
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException($"Could not create seed Owner: {string.Join(", ", createResult.Errors.Select(x => x.Description))}");
                }

                createdNow = true;
            }

            if (!await userManager.IsInRoleAsync(owner, AppRoles.Owner))
            {
                var roleResult = await userManager.AddToRoleAsync(owner, AppRoles.Owner);
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException($"Could not assign Owner role: {string.Join(", ", roleResult.Errors.Select(x => x.Description))}");
                }
            }

            var currentPermissions = await permissionService.GetPermissionsAsync(owner);
            if (createdNow || currentPermissions.Count == 0)
            {
                var permissionResult = await permissionService.SetDefaultsForRoleAsync(owner, AppRoles.Owner);
                if (!permissionResult.Succeeded)
                {
                    throw new InvalidOperationException($"Could not seed Owner permissions: {string.Join(", ", permissionResult.Errors.Select(x => x.Description))}");
                }
            }
        }
        else
        {
            logger.LogWarning("No seed Owner credentials configured. Existing users/roles will still be available, but a first Owner must be created/configured before initial use.");
        }

        // Owners are Super Admins. When later modules add a new protected permission,
        // existing Owner accounts receive that permission without losing other claims.
        var ownerUsers = await userManager.GetUsersInRoleAsync(AppRoles.Owner);
        var ownerDefaults = AppPermissions.DefaultsForRole(AppRoles.Owner);
        foreach (var ownerUser in ownerUsers)
        {
            var permissions = await permissionService.GetPermissionsAsync(ownerUser);
            var merged = permissions.Union(ownerDefaults, StringComparer.OrdinalIgnoreCase).ToArray();
            if (merged.Length != permissions.Count)
            {
                var mergeResult = await permissionService.SetPermissionsAsync(ownerUser, merged);
                if (!mergeResult.Succeeded)
                {
                    throw new InvalidOperationException($"Could not upgrade Owner permissions: {string.Join(", ", mergeResult.Errors.Select(x => x.Description))}");
                }

                await userManager.UpdateSecurityStampAsync(ownerUser);
            }
        }
    }
}
