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
public class CreateModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserPermissionService _permissionService;

    public CreateModel(UserManager<ApplicationUser> userManager, UserPermissionService permissionService)
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
        [Required, StringLength(120), Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = AppRoles.Salesman;

        [Required, DataType(DataType.Password), Display(Name = "Temporary password")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password)), Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Require password change at next sign-in")]
        public bool MustChangePassword { get; set; } = true;
    }

    public void OnGet()
    {
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

        var currentUserId = _userManager.GetUserId(User);
        var email = Input.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = Input.FullName.Trim(),
            IsActive = true,
            MustChangePassword = Input.MustChangePassword,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        var roleResult = await _userManager.AddToRoleAsync(user, Input.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in roleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        var permissionResult = await _permissionService.SetDefaultsForRoleAsync(user, Input.Role);
        if (!permissionResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            foreach (var error in permissionResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }


        TempData["SuccessMessage"] = $"User '{user.FullName}' created successfully.";
        return RedirectToPage("Index");
    }
}
