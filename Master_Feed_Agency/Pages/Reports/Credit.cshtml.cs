using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Reports;

[Authorize(Policy = AppPermissions.ViewReports)]
public sealed class CreditModel : PageModel
{
    private readonly ReportService _reports;
    public CreditModel(ReportService reports) => _reports = reports;

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public CreditAgingBucket Aging { get; set; } = CreditAgingBucket.All;
    public CreditDashboard Report { get; private set; } = CreditDashboard.Empty(BusinessTime.Today);

    public async Task OnGetAsync(CancellationToken ct) => Report = await _reports.GetCreditAsync(Search, Aging, ct);

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        Report = await _reports.GetCreditAsync(Search, Aging, ct);
        var rows = new List<IEnumerable<object?>>
        {
            new object?[] { "Customer", "Phone", "Reference", "Source Date", "Due Date", "Outstanding", "Days Overdue", "Status", "Aging" }
        };
        rows.AddRange(Report.Rows.Select(x => (IEnumerable<object?>)new object?[]
        {
            x.CustomerName, x.Phone, x.Reference, x.SourceDate, x.DueDate?.ToString("yyyy-MM-dd"),
            x.OutstandingAmount, x.DaysOverdue, x.Status, x.AgingBucket
        }));
        return File(ReportCsv.Build(rows), "text/csv; charset=utf-8", $"outstanding-credit-{Report.AsOfDate:yyyyMMdd}.csv");
    }
}
