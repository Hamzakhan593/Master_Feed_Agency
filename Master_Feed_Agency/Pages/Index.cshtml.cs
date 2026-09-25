using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IAuthorizationService _authorization;

    public IndexModel(ILogger<IndexModel> logger, IAuthorizationService authorization)
    {
        _logger = logger;
        _authorization = authorization;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Users who can see the business dashboard should land there directly.
        // This keeps the post-login experience focused and avoids an unnecessary launcher page.
        if (User.Identity?.IsAuthenticated == true)
        {
            var dashboardAccess = await _authorization.AuthorizeAsync(User, AppPermissions.ViewDashboard);
            if (dashboardAccess.Succeeded)
            {
                return RedirectToPage("/Dashboard/Index");
            }
        }

        return Page();
    }
}
