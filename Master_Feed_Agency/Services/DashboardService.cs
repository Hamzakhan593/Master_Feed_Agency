using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

/// <summary>
/// Simple read-only business summary for the agency owner/staff. The dashboard intentionally
/// focuses on the few things used every day: sales, customer udhaar, payments and stock.
/// Detailed customer history stays inside the customer khata instead of being repeated here.
/// </summary>
public sealed class DashboardService
{
    private readonly ApplicationDbContext _db;
    private readonly CreditService _credit;

    public DashboardService(ApplicationDbContext db, CreditService credit)
    {
        _db = db;
        _credit = credit;
    }

    public async Task<OwnerDashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var today = BusinessTime.Today;
        var fromUtc = BusinessTime.ToUtcStart(today);
        var toUtcExclusive = BusinessTime.ToUtcEndExclusive(today);

        // Keep EF Core operations sequential because all services in this request share one scoped DbContext.
        var credit = await _credit.GetDashboardAsync(new CreditQuery(), ct);

        var todaySales = await _db.Sales.AsNoTracking()
            .Where(x => x.Status == SaleStatus.Posted && x.SaleDate >= fromUtc && x.SaleDate < toUtcExclusive)
            .SumAsync(x => (decimal?)x.NetTotal, ct) ?? 0m;

        var todayRecovery = await _db.Payments.AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Posted && x.PaymentDate >= fromUtc && x.PaymentDate < toUtcExclusive)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var todaySaleReceipts = await _db.Sales.AsNoTracking()
            .Where(x => x.Status == SaleStatus.Posted && x.SaleDate >= fromUtc && x.SaleDate < toUtcExclusive)
            .SumAsync(x => (decimal?)x.PaidAtSale, ct) ?? 0m;

        var stock = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Unit,
                x.LowStockThreshold,
                CurrentStock = x.StockTransactions.Sum(t => (decimal?)(t.QuantityIn - t.QuantityOut)) ?? 0m
            })
            .ToListAsync(ct);

        var lowStock = stock
            .Where(x => x.CurrentStock <= x.LowStockThreshold)
            .OrderBy(x => x.CurrentStock - x.LowStockThreshold)
            .ThenBy(x => x.Name)
            .Take(6)
            .Select(x => new DashboardLowStockRow(
                x.Id, x.Code, x.Name, x.Unit, RoundQuantity(x.CurrentStock), RoundQuantity(x.LowStockThreshold)))
            .ToList();

        var dueToday = credit.Rows
            .Where(x => x.Status == CreditStatus.DueToday || x.Status == CreditStatus.Overdue)
            .GroupBy(x => new { x.CustomerId, x.CustomerName, x.Phone })
            .Select(g => new DashboardDueCustomerRow(
                g.Key.CustomerId,
                g.Key.CustomerName,
                g.Key.Phone,
                RoundMoney(g.Sum(x => x.OutstandingAmount)),
                g.Count()))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.CustomerName)
            .Take(8)
            .ToList();

        return new OwnerDashboardSnapshot(
            today,
            credit.Summary.TotalOutstanding,
            credit.Summary.TodayDue,
            credit.Summary.Overdue,
            RoundMoney(todaySales),
            RoundMoney(todayRecovery),
            RoundMoney(todaySaleReceipts),
            stock.Count,
            stock.Count(x => x.CurrentStock <= x.LowStockThreshold),
            credit.Summary.CustomerCount,
            dueToday,
            lowStock);
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal RoundQuantity(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record OwnerDashboardSnapshot(
    DateTime BusinessDate,
    decimal TotalUdhaar,
    decimal TodayDue,
    decimal Overdue,
    decimal TodaySales,
    decimal TodayRecovery,
    decimal TodaySaleReceipts,
    int ActiveProductCount,
    int LowStockCount,
    int OutstandingCustomerCount,
    IReadOnlyList<DashboardDueCustomerRow> DueCustomers,
    IReadOnlyList<DashboardLowStockRow> LowStockProducts);

public sealed record DashboardDueCustomerRow(int CustomerId, string CustomerName, string Phone, decimal Amount, int ObligationCount);

public sealed record DashboardLowStockRow(int ProductId, string Code, string Name, string Unit, decimal CurrentStock, decimal Threshold);
