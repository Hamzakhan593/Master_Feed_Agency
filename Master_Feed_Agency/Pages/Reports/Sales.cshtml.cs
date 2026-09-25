using System.ComponentModel.DataAnnotations;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Reports;

[Authorize(Policy = AppPermissions.ViewReports)]
public sealed class SalesModel : PageModel
{
    private readonly ReportService _reports;
    public SalesModel(ReportService reports) => _reports = reports;

    [BindProperty(SupportsGet = true), DataType(DataType.Date)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true), DataType(DataType.Date)] public DateTime? To { get; set; }
    public SalesReportData Report { get; private set; } = new([], 0, 0, 0, 0, 0, 0, 0, 0);

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        var rows = new List<IEnumerable<object?>>
        {
            new object?[] { "Date", "Invoice", "Customer", "Sale Type", "Gross", "Discount", "Net", "Paid At Sale", "Credit", "Status", "Created By" }
        };
        rows.AddRange(Report.Rows.Select(x => (IEnumerable<object?>)new object?[]
        {
            x.Date, x.InvoiceNo, x.Customer, x.SaleType, x.GrossTotal, x.Discount, x.NetTotal,
            x.PaidAtSale, x.CreditAmount, x.Status, x.CreatedBy
        }));
        return File(ReportCsv.Build(rows), "text/csv; charset=utf-8", $"sales-report-{From!.Value:yyyyMMdd}-{To!.Value:yyyyMMdd}.csv");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var p = ReportService.NormalizePeriod(From, To);
        From = p.From; To = p.To;
        Report = await _reports.GetSalesAsync(p.From, p.To, ct);
    }
}
