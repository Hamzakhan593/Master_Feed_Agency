using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Reports;

[Authorize(Policy = AppPermissions.ViewReports)]
public sealed class IndexModel : PageModel
{
    public void OnGet() { }
}
