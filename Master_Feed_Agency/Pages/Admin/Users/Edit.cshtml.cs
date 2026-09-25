using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Pages.Admin.Users;

[Authorize(Policy = AppPermissions.ManageUsers)]
public class EditModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserPermissionService _permissionService;

    public EditModel(UserManager<ApplicationUser> userManager, UserPermissionService permissionService)
    {
        _userManager = userManager;
        _permissionService = permissionService;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IEnumerable<SelectListItem> RoleOptions =>
        AppRoles.All.Select(x => new SelectListItem(x, x));

    public class InputModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required, StringLength(120), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty;

        [Display(Name = "Account is active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        Input = new InputModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? AppRoles.Salesman,
            IsActive = user.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!AppRoles.All.Contains(Input.Role, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("Input.Role", "Select a valid role.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByIdAsync(Input.Id);
        if (user is null)
        {
            return NotFound();
        }

        var currentUserId = _userManager.GetUserId(User);
        var oldRoles = await _userManager.GetRolesAsync(user);
        var oldRole = oldRoles.FirstOrDefault();
        var before = new { user.FullName, user.Email, Role = oldRole, user.IsActive };
        var roleChanged = !string.Equals(oldRole, Input.Role, StringComparison.OrdinalIgnoreCase);
        var disabling = user.IsActive && !Input.IsActive;
        var removingOwner = string.Equals(oldRole, AppRoles.Owner, StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(Input.Role, AppRoles.Owner, StringComparison.OrdinalIgnoreCase);

        if (user.Id == currentUserId && disabling)
        {
            ModelState.AddModelError(string.Empty, "You cannot disable your own account.");
            return Page();
        }

        if ((disabling || removingOwner) && await _userManager.IsInRoleAsync(user, AppRoles.Owner))
        {
            var owners = await _userManager.GetUsersInRoleAsync(AppRoles.Owner);
            if (owners.Count(x => x.IsActive) <= 1)
            {
                ModelState.AddModelError(string.Empty, "The last active Owner account must remain active and keep the Owner role.");
                return Page();
            }
        }

        user.FullName = Input.FullName.Trim();
        user.Email = Input.Email.Trim();
        user.UserName = Input.Email.Trim();
        user.IsActive = Input.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedByUserId = currentUserId;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        if (roleChanged)
        {
            if (oldRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, oldRoles);
                if (!removeResult.Succeeded)
                {
                    foreach (var error in removeResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    return Page();
                }
            }

            var addRoleResult = await _userManager.AddToRoleAsync(user, Input.Role);
            if (!addRoleResult.Succeeded)
            {
                foreach (var error in addRoleResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            var permissionResult = await _permissionService.SetDefaultsForRoleAsync(user, Input.Role);
            if (!permissionResult.Succeeded)
            {
                foreach (var error in permissionResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }
        }

        if (roleChanged || !user.IsActive)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }


        TempData["SuccessMessage"] = $"User '{user.FullName}' updated successfully.";
        return RedirectToPage("Index");
    }
}
