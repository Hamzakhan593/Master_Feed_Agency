using System.ComponentModel.DataAnnotations;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Reports;

[Authorize(Policy = AppPermissions.ViewReports)]
public sealed class StockModel : PageModel
{
    private readonly ReportService _reports;
    public StockModel(ReportService reports) => _reports = reports;

    [BindProperty(SupportsGet = true), DataType(DataType.Date)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true), DataType(DataType.Date)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public bool LowStockOnly { get; set; }
    public StockReportData Report { get; private set; } = new([], 0, 0, 0);
    public IReadOnlyList<StockReportRow> VisibleRows => LowStockOnly ? Report.Rows.Where(x => x.IsActive && x.ClosingStock <= x.LowStockThreshold).ToList() : Report.Rows;

    public async Task OnGetAsync(CancellationToken ct) => await LoadAsync(ct);

    public async Task<IActionResult> OnGetCsvAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        var rows = new List<IEnumerable<object?>>
        {
            new object?[] { "Code", "Product", "Unit", "Opening Stock", "Stock In", "Stock Out", "Closing Stock", "Low Stock Threshold", "Sale Rate", "Sale Value", "Active" }
        };
        rows.AddRange(VisibleRows.Select(x => (IEnumerable<object?>)new object?[]
        { x.Code, x.Name, x.Unit, x.OpeningStock, x.QuantityIn, x.QuantityOut, x.ClosingStock, x.LowStockThreshold, x.SalePrice, x.SaleValue, x.IsActive }));
        return File(ReportCsv.Build(rows), "text/csv; charset=utf-8", $"stock-report-{From!.Value:yyyyMMdd}-{To!.Value:yyyyMMdd}.csv");
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var p = ReportService.NormalizePeriod(From, To);
        From = p.From; To = p.To;
        Report = await _reports.GetStockAsync(p.From, p.To, ct);
    }
}
