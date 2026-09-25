using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

public sealed class ReportService
{
    private readonly ApplicationDbContext _db;
    private readonly CreditService _credit;

    public ReportService(ApplicationDbContext db, CreditService credit)
    {
        _db = db;
        _credit = credit;
    }

    public static ReportPeriod NormalizePeriod(DateTime? from, DateTime? to)
    {
        var today = BusinessTime.Today;
        var start = from?.Date ?? new DateTime(today.Year, today.Month, 1);
        var end = to?.Date ?? today;
        if (start > end) (start, end) = (end, start);
        return new ReportPeriod(start, end);
    }

    public async Task<SalesReportData> GetSalesAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var start = BusinessTime.ToUtcStart(from);
        var end = BusinessTime.ToUtcEndExclusive(to);
        var raw = await _db.Sales.AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.SaleDate >= start && x.SaleDate < end)
            .OrderByDescending(x => x.SaleDate).ThenByDescending(x => x.Id)
            .ToListAsync(ct);

        var userIds = raw.Select(x => x.CreatedByUserId).Distinct().ToArray();
        var names = await UserNamesAsync(userIds, ct);
        var rows = raw.Select(x => new SalesReportRow(
            x.Id, x.InvoiceNo, x.SaleDate, x.Customer?.Name ?? "Walk-in Customer", x.SaleType,
            x.GrossTotal, x.Discount, x.NetTotal, x.PaidAtSale, x.CreditAmount, x.Status,
            names.GetValueOrDefault(x.CreatedByUserId, x.CreatedByUserId))).ToList();

        var posted = raw.Where(x => x.Status == SaleStatus.Posted).ToList();
        return new SalesReportData(
            rows,
            posted.Count,
            raw.Count(x => x.Status == SaleStatus.Cancelled),
            Round(posted.Sum(x => x.GrossTotal)),
            Round(posted.Sum(x => x.Discount)),
            Round(posted.Sum(x => x.NetTotal)),
            Round(posted.Where(x => x.SaleType == SaleType.Cash).Sum(x => x.NetTotal)),
            Round(posted.Sum(x => x.CreditAmount)),
            Round(posted.Sum(x => x.PaidAtSale)));
    }

    public Task<CreditDashboard> GetCreditAsync(string? search, CreditAgingBucket aging, CancellationToken ct = default)
        => _credit.GetDashboardAsync(new CreditQuery(CreditView.All, CreditSort.Priority, aging, search), ct);

    public async Task<StockReportData> GetStockAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var start = BusinessTime.ToUtcStart(from);
        var end = BusinessTime.ToUtcEndExclusive(to);
        var products = await _db.Products.AsNoTracking()
            .OrderBy(x => x.Name).ThenBy(x => x.Code)
            .ToListAsync(ct);
        var ids = products.Select(x => x.Id).ToArray();
        List<StockTransaction> movements = ids.Length == 0
            ? new List<StockTransaction>()
            : await _db.StockTransactions.AsNoTracking()
                .Where(x => ids.Contains(x.ProductId) && x.Date < end)
                .ToListAsync(ct);

        var byProduct = movements.GroupBy(x => x.ProductId).ToDictionary(x => x.Key, x => x.ToList());
        var rows = new List<StockReportRow>(products.Count);
        foreach (var product in products)
        {
            var txs = byProduct.GetValueOrDefault(product.Id) ?? new List<StockTransaction>();
            var opening = txs.Where(x => x.Date < start).Sum(x => x.QuantityIn - x.QuantityOut);
            var period = txs.Where(x => x.Date >= start && x.Date < end).ToList();
            var qtyIn = period.Sum(x => x.QuantityIn);
            var qtyOut = period.Sum(x => x.QuantityOut);
            var closing = opening + qtyIn - qtyOut;
            rows.Add(new StockReportRow(
                product.Id, product.Code, product.Name, product.Unit, product.IsActive,
                product.LowStockThreshold, product.DefaultSalePrice, product.PurchaseCost,
                RoundQty(opening), RoundQty(qtyIn), RoundQty(qtyOut), RoundQty(closing),
                Round(closing * product.DefaultSalePrice)));
        }

        return new StockReportData(
            rows,
            rows.Count(x => x.IsActive),
            rows.Count(x => x.IsActive && x.ClosingStock <= x.LowStockThreshold),
            Round(rows.Where(x => x.IsActive).Sum(x => x.SaleValue)));
    }

    private async Task<Dictionary<string, string>> UserNamesAsync(string[] userIds, CancellationToken ct)
    {
        if (userIds.Length == 0) return new Dictionary<string, string>();
        return await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundQty(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record ReportPeriod(DateTime From, DateTime To);

public sealed record SalesReportRow(long Id, string InvoiceNo, DateTime Date, string Customer, SaleType SaleType,
    decimal GrossTotal, decimal Discount, decimal NetTotal, decimal PaidAtSale, decimal CreditAmount, SaleStatus Status, string CreatedBy);
public sealed record SalesReportData(IReadOnlyList<SalesReportRow> Rows, int PostedCount, int CancelledCount,
    decimal GrossTotal, decimal Discount, decimal NetTotal, decimal CashSaleValue, decimal CreditIssued, decimal PaidAtSale);

public sealed record StockReportRow(int ProductId, string Code, string Name, string Unit, bool IsActive,
    decimal LowStockThreshold, decimal SalePrice, decimal? PurchaseCost, decimal OpeningStock, decimal QuantityIn,
    decimal QuantityOut, decimal ClosingStock, decimal SaleValue);
public sealed record StockReportData(IReadOnlyList<StockReportRow> Rows, int ActiveProducts, int LowStockCount, decimal StockValueAtSaleRate);

