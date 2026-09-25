using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Master_Feed_Agency.Services;

public class UserPermissionService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserPermissionService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(ApplicationUser user)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        return claims
            .Where(x => x.Type == AppPermissions.ClaimType)
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IdentityResult> SetPermissionsAsync(ApplicationUser user, IEnumerable<string> selectedPermissions)
    {
        var allowed = AppPermissions.All.Select(x => x.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selected = selectedPermissions
            .Where(allowed.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var existingClaims = await _userManager.GetClaimsAsync(user);
        var permissionClaims = existingClaims.Where(x => x.Type == AppPermissions.ClaimType).ToArray();

        if (permissionClaims.Length > 0)
        {
            var removeResult = await _userManager.RemoveClaimsAsync(user, permissionClaims);
            if (!removeResult.Succeeded)
            {
                return removeResult;
            }
        }

        if (selected.Length == 0)
        {
            return IdentityResult.Success;
        }

        return await _userManager.AddClaimsAsync(
            user,
            selected.Select(x => new Claim(AppPermissions.ClaimType, x)));
    }

    public Task<IdentityResult> SetDefaultsForRoleAsync(ApplicationUser user, string role) =>
        SetPermissionsAsync(user, AppPermissions.DefaultsForRole(role));
}
