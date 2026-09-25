using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Dashboard;

[Authorize(Policy = AppPermissions.ViewDashboard)]
public sealed class IndexModel : PageModel
{
    private readonly DashboardService _dashboard;

    public IndexModel(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public OwnerDashboardSnapshot Snapshot { get; private set; } = null!;

    public async Task OnGetAsync(CancellationToken ct)
    {
        Snapshot = await _dashboard.GetSnapshotAsync(ct);
    }
}
