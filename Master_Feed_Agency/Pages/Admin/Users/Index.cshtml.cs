using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Admin.Users;

[Authorize(Policy = AppPermissions.ManageUsers)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public List<UserRow> Users { get; private set; } = [];

    public sealed record UserRow(string Id, string FullName, string Email, string Role, bool IsActive, int PermissionCount);

    public async Task OnGetAsync()
    {
        var users = await _userManager.Users.OrderBy(x => x.FullName).ToListAsync();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            Users.Add(new UserRow(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                roles.FirstOrDefault() ?? "No role",
                user.IsActive,
                claims.Count(x => x.Type == AppPermissions.ClaimType)));
        }
    }

    public async Task<IActionResult> OnPostToggleStatusAsync(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        if (target.Id == currentUserId)
        {
            TempData["ErrorMessage"] = "You cannot disable your own account.";
            return RedirectToPage();
        }

        if (target.IsActive && await _userManager.IsInRoleAsync(target, AppRoles.Owner))
        {
            var owners = await _userManager.GetUsersInRoleAsync(AppRoles.Owner);
            if (owners.Count(x => x.IsActive) <= 1)
            {
                TempData["ErrorMessage"] = "The last active Owner account cannot be disabled.";
                return RedirectToPage();
            }
        }

        var wasActive = target.IsActive;
        target.IsActive = !target.IsActive;
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedByUserId = currentUserId;

        var result = await _userManager.UpdateAsync(target);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
            return RedirectToPage();
        }

        if (!target.IsActive)
        {
            await _userManager.UpdateSecurityStampAsync(target);
        }


        TempData["SuccessMessage"] = target.IsActive
            ? $"{target.FullName} has been enabled."
            : $"{target.FullName} has been disabled.";

        return RedirectToPage();
    }
}
