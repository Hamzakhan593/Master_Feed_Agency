using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Pages.Admin.Users;

[Authorize(Policy = AppPermissions.ManageUsers)]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ResetPasswordModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string TargetUserName { get; private set; } = string.Empty;

    public class InputModel
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "New temporary password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword)), Display(Name = "Confirm temporary password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var target = await _userManager.FindByIdAsync(id);
        if (target is null)
        {
            return NotFound();
        }

        if (target.Id == _userManager.GetUserId(User))
        {
            TempData["ErrorMessage"] = "Use Change Password for your own account instead of administrative reset.";
            return RedirectToPage("Index");
        }

        TargetUserName = target.FullName;
        Input.UserId = target.Id;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var target = await _userManager.FindByIdAsync(Input.UserId);
        if (target is null)
        {
            return NotFound();
        }

        TargetUserName = target.FullName;

        if (target.Id == _userManager.GetUserId(User))
        {
            ModelState.AddModelError(string.Empty, "Use Change Password for your own account.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(target);
        var result = await _userManager.ResetPasswordAsync(target, token, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }

        target.MustChangePassword = true;
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedByUserId = _userManager.GetUserId(User);
        await _userManager.UpdateAsync(target);
        await _userManager.UpdateSecurityStampAsync(target);


        TempData["SuccessMessage"] = $"Password reset for {target.FullName}. They must change it at next sign-in.";
        return RedirectToPage("Index");
    }
}
