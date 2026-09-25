using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Admin.Users;

[Authorize(Policy = AppPermissions.ManageUsers)]
public class PermissionsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserPermissionService _permissionService;

    public PermissionsModel(UserManager<ApplicationUser> userManager, UserPermissionService permissionService)
    {
        _userManager = userManager;
        _permissionService = permissionService;
    }

    [BindProperty]
    public string TargetUserId { get; set; } = string.Empty;

    [BindProperty]
    public List<string> SelectedPermissions { get; set; } = [];

    public string TargetUserName { get; private set; } = string.Empty;
    public string TargetRole { get; private set; } = string.Empty;
    public List<PermissionOption> PermissionOptions { get; private set; } = [];

    public sealed record PermissionOption(string Value, string Label, string Group, bool IsSelected)
    {
        public string SafeId => Value.Replace('.', '_');
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        TargetUserId = id;
        return await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var target = await _userManager.FindByIdAsync(TargetUserId);
        if (target is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        var beforePermissions = await _permissionService.GetPermissionsAsync(target);
        if (target.Id == currentUserId && !SelectedPermissions.Contains(AppPermissions.ManageUsers, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(string.Empty, "You cannot remove your own user-management permission because that would lock you out of this screen.");
            return await LoadAsync(TargetUserId);
        }

        var result = await _permissionService.SetPermissionsAsync(target, SelectedPermissions);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return await LoadAsync(TargetUserId);
        }

        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedByUserId = currentUserId;
        await _userManager.UpdateAsync(target);
        await _userManager.UpdateSecurityStampAsync(target);


        TempData["SuccessMessage"] = $"Permissions updated for {target.FullName}.";
        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostResetDefaultsAsync(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(target);
        var role = roles.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(role))
        {
            TempData["ErrorMessage"] = "This user has no role, so default permissions cannot be restored.";
            return RedirectToPage("Index");
        }

        var beforePermissions = await _permissionService.GetPermissionsAsync(target);
        var result = await _permissionService.SetDefaultsForRoleAsync(target, role);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(x => x.Description));
            return RedirectToPage("Index");
        }

        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedByUserId = _userManager.GetUserId(User);
        await _userManager.UpdateAsync(target);
        await _userManager.UpdateSecurityStampAsync(target);

        var afterPermissions = await _permissionService.GetPermissionsAsync(target);

        TempData["SuccessMessage"] = $"{target.FullName}'s permissions were reset to the {role} defaults.";
        return RedirectToPage("Index");
    }

    private async Task<IActionResult> LoadAsync(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(target);
        var selected = (await _permissionService.GetPermissionsAsync(target))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        TargetUserId = target.Id;
        TargetUserName = target.FullName;
        TargetRole = roles.FirstOrDefault() ?? "No role";
        PermissionOptions = AppPermissions.All
            .Select(x => new PermissionOption(x.Value, x.Label, x.Group, selected.Contains(x.Value)))
            .ToList();

        return Page();
    }
}
