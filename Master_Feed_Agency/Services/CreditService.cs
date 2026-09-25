using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

/// <summary>
/// Read model for udhaar / receivables. It uses the customer ledger as the balance source of truth,
/// then allocates reductions to the oldest obligations so due/aging views remain reconciled.
/// Module 7 can later replace the inferred allocation with explicit payment allocations without
/// changing the Credit pages.
/// </summary>
public sealed class CreditService
{
    private readonly ApplicationDbContext _db;

    public CreditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CreditDashboard> GetDashboardAsync(
        CreditQuery query,
        CancellationToken cancellationToken = default)
    {
        var today = BusinessTime.Today;

        var customers = await _db.Customers
            .AsNoTracking()
            .Select(x => new CreditCustomer(
                x.Id,
                x.Name,
                x.BusinessName,
                x.Phone,
                x.CreditLimit,
                x.IsActive))
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return CreditDashboard.Empty(today);
        }

        var customerIds = customers.Select(x => x.Id).ToArray();

        var balances = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId))
            .GroupBy(x => x.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Balance = g.Sum(x => x.Debit - x.Credit)
            })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Balance, cancellationToken);

        var openingBalances = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.CustomerId) &&
                        x.Type == CustomerLedgerEntryType.OpeningBalance)
            .GroupBy(x => x.CustomerId)
            .Select(g => new
            {
                CustomerId = g.Key,
                Amount = g.Sum(x => x.Debit - x.Credit),
                Date = g.Min(x => x.Date)
            })
            .ToDictionaryAsync(x => x.CustomerId, x => new OpeningBalanceInfo(x.Amount, x.Date), cancellationToken);

        var creditSales = await _db.Sales
            .AsNoTracking()
            .Where(x => x.Status == SaleStatus.Posted &&
                        x.CustomerId.HasValue &&
                        x.CreditAmount > 0m)
            .OrderBy(x => x.SaleDate)
            .ThenBy(x => x.Id)
            .Select(x => new CreditSaleInfo(
                x.Id,
                x.InvoiceNo,
                x.CustomerId.GetValueOrDefault(),
                x.SaleDate,
                x.DueDate,
                x.CreditAmount))
            .ToListAsync(cancellationToken);

        var salesByCustomer = creditSales
            .GroupBy(x => x.CustomerId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var allRows = new List<CreditObligationRow>();

        foreach (var customer in customers)
        {
            var ledgerBalance = balances.GetValueOrDefault(customer.Id);
            if (ledgerBalance <= 0m)
            {
                continue;
            }

            var raw = new List<RawObligation>();

            if (openingBalances.TryGetValue(customer.Id, out var opening) && opening.Amount > 0m)
            {
                raw.Add(new RawObligation(
                    null,
                    "Opening balance",
                    opening.Date,
                    null,
                    opening.Amount,
                    CreditSourceType.OpeningBalance));
            }

            if (salesByCustomer.TryGetValue(customer.Id, out var sales))
            {
                foreach (var sale in sales)
                {
                    raw.Add(new RawObligation(
                        sale.SaleId,
                        sale.InvoiceNo,
                        sale.SaleDate,
                        sale.DueDate?.Date,
                        sale.CreditAmount,
                        CreditSourceType.Sale));
                }
            }

            var baseTotal = raw.Sum(x => x.Amount);
            var reductionToAllocate = Math.Max(0m, baseTotal - ledgerBalance);

            // Oldest-first is the default allocation model documented for the project.
            // Opening balance is intentionally first because it predates system invoices.
            var ordered = raw
                .OrderBy(x => x.SourceType == CreditSourceType.OpeningBalance ? 0 : 1)
                .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
                .ThenBy(x => x.SourceDate)
                .ThenBy(x => x.SaleId ?? 0L)
                .ToList();

            decimal allocatedOutstanding = 0m;
            foreach (var obligation in ordered)
            {
                var applied = Math.Min(obligation.Amount, reductionToAllocate);
                reductionToAllocate -= applied;
                var outstanding = RoundMoney(obligation.Amount - applied);
                if (outstanding <= 0m)
                {
                    continue;
                }

                allocatedOutstanding += outstanding;
                allRows.Add(CreateRow(customer, obligation, outstanding, today));
            }

            // Positive ledger adjustments or legacy transactions may create balance that is not tied to
            // an invoice. Keep it visible instead of hiding a reconciliation difference.
            var unmapped = RoundMoney(ledgerBalance - allocatedOutstanding);
            if (unmapped > 0m)
            {
                allRows.Add(CreateRow(
                    customer,
                    new RawObligation(
                        null,
                        "Unscheduled balance",
                        DateTime.UtcNow,
                        null,
                        unmapped,
                        CreditSourceType.Unscheduled),
                    unmapped,
                    today));
            }
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        IEnumerable<CreditObligationRow> filtered = allRows;

        if (search is not null)
        {
            filtered = filtered.Where(x =>
                x.CustomerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(x.BusinessName) && x.BusinessName.Contains(search, StringComparison.OrdinalIgnoreCase)) ||
                x.Phone.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Reference.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        filtered = query.View switch
        {
            CreditView.TodayDue => filtered.Where(x => x.DueDate == today),
            CreditView.Upcoming => filtered.Where(x => x.DueDate.HasValue && x.DueDate.Value > today),
            CreditView.Overdue => filtered.Where(x => x.DueDate.HasValue && x.DueDate.Value < today),
            _ => filtered
        };

        filtered = query.AgingBucket switch
        {
            CreditAgingBucket.Current => filtered.Where(x => x.AgingBucket == CreditAgingBucket.Current),
            CreditAgingBucket.Days1To7 => filtered.Where(x => x.AgingBucket == CreditAgingBucket.Days1To7),
            CreditAgingBucket.Days8To30 => filtered.Where(x => x.AgingBucket == CreditAgingBucket.Days8To30),
            CreditAgingBucket.Days31To60 => filtered.Where(x => x.AgingBucket == CreditAgingBucket.Days31To60),
            CreditAgingBucket.Days60Plus => filtered.Where(x => x.AgingBucket == CreditAgingBucket.Days60Plus),
            _ => filtered
        };

        filtered = query.Sort switch
        {
            CreditSort.Amount => filtered.OrderByDescending(x => x.OutstandingAmount).ThenBy(x => x.DueDate),
            CreditSort.Age => filtered.OrderByDescending(x => x.DaysOverdue).ThenByDescending(x => x.OutstandingAmount),
            CreditSort.Customer => filtered.OrderBy(x => x.CustomerName).ThenBy(x => x.DueDate),
            CreditSort.DueDate => filtered.OrderBy(x => x.DueDate ?? DateTime.MaxValue).ThenByDescending(x => x.OutstandingAmount),
            _ => filtered
                .OrderBy(x => x.DueDate.HasValue ? 0 : 1)
                .ThenBy(x => x.DueDate ?? DateTime.MaxValue)
                .ThenByDescending(x => x.OutstandingAmount)
        };

        var endOfMonth = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
        var next7 = today.AddDays(6);

        var summary = new CreditSummary(
            TotalOutstanding: RoundMoney(allRows.Sum(x => x.OutstandingAmount)),
            TodayDue: RoundMoney(allRows.Where(x => x.DueDate == today).Sum(x => x.OutstandingAmount)),
            Overdue: RoundMoney(allRows.Where(x => x.DueDate.HasValue && x.DueDate.Value < today).Sum(x => x.OutstandingAmount)),
            DueNext7Days: RoundMoney(allRows.Where(x => x.DueDate.HasValue && x.DueDate.Value >= today && x.DueDate.Value <= next7).Sum(x => x.OutstandingAmount)),
            DueThisMonth: RoundMoney(allRows.Where(x => x.DueDate.HasValue && x.DueDate.Value >= today && x.DueDate.Value <= endOfMonth).Sum(x => x.OutstandingAmount)),
            Unscheduled: RoundMoney(allRows.Where(x => !x.DueDate.HasValue).Sum(x => x.OutstandingAmount)),
            CustomerCount: allRows.Select(x => x.CustomerId).Distinct().Count());

        var aging = Enum.GetValues<CreditAgingBucket>()
            .Where(x => x != CreditAgingBucket.All)
            .Select(bucket => new CreditAgingSummary(
                bucket,
                allRows.Where(x => x.AgingBucket == bucket).Sum(x => x.OutstandingAmount),
                allRows.Where(x => x.AgingBucket == bucket).Select(x => x.CustomerId).Distinct().Count()))
            .ToList();

        var creditLimitWarnings = customers
            .Select(x => new
            {
                Customer = x,
                Balance = Math.Max(0m, balances.GetValueOrDefault(x.Id))
            })
            .Where(x => x.Customer.CreditLimit > 0m && x.Balance >= x.Customer.CreditLimit)
            .OrderByDescending(x => x.Balance - x.Customer.CreditLimit)
            .Select(x => new CreditLimitWarning(
                x.Customer.Id,
                x.Customer.Name,
                x.Balance,
                x.Customer.CreditLimit,
                RoundMoney(x.Balance - x.Customer.CreditLimit)))
            .ToList();

        return new CreditDashboard(
            today,
            summary,
            aging,
            filtered.ToList(),
            creditLimitWarnings);
    }

    private static CreditObligationRow CreateRow(
        CreditCustomer customer,
        RawObligation obligation,
        decimal outstanding,
        DateTime today)
    {
        var daysOverdue = obligation.DueDate.HasValue && obligation.DueDate.Value < today
            ? (today - obligation.DueDate.Value).Days
            : 0;

        var status = !obligation.DueDate.HasValue
            ? CreditStatus.Unscheduled
            : obligation.DueDate.Value < today
                ? CreditStatus.Overdue
                : obligation.DueDate.Value == today
                    ? CreditStatus.DueToday
                    : CreditStatus.Upcoming;

        var bucket = daysOverdue switch
        {
            <= 0 => CreditAgingBucket.Current,
            <= 7 => CreditAgingBucket.Days1To7,
            <= 30 => CreditAgingBucket.Days8To30,
            <= 60 => CreditAgingBucket.Days31To60,
            _ => CreditAgingBucket.Days60Plus
        };

        return new CreditObligationRow(
            customer.Id,
            customer.Name,
            customer.BusinessName,
            customer.Phone,
            customer.IsActive,
            customer.CreditLimit,
            obligation.SaleId,
            obligation.Reference,
            obligation.SourceDate,
            obligation.DueDate,
            outstanding,
            daysOverdue,
            status,
            bucket,
            obligation.SourceType);
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record CreditCustomer(
        int Id,
        string Name,
        string? BusinessName,
        string Phone,
        decimal CreditLimit,
        bool IsActive);

    private sealed record OpeningBalanceInfo(decimal Amount, DateTime Date);
    private sealed record CreditSaleInfo(long SaleId, string InvoiceNo, int CustomerId, DateTime SaleDate, DateTime? DueDate, decimal CreditAmount);
    private sealed record RawObligation(long? SaleId, string Reference, DateTime SourceDate, DateTime? DueDate, decimal Amount, CreditSourceType SourceType);
}

public sealed record CreditQuery(
    CreditView View = CreditView.All,
    CreditSort Sort = CreditSort.Priority,
    CreditAgingBucket AgingBucket = CreditAgingBucket.All,
    string? Search = null);

public enum CreditView
{
    All = 0,
    TodayDue = 1,
    Upcoming = 2,
    Overdue = 3
}

public enum CreditSort
{
    Priority = 0,
    DueDate = 1,
    Amount = 2,
    Age = 3,
    Customer = 4
}

public enum CreditStatus
{
    Unscheduled = 0,
    Upcoming = 1,
    DueToday = 2,
    Overdue = 3
}

public enum CreditAgingBucket
{
    All = 0,
    Current = 1,
    Days1To7 = 2,
    Days8To30 = 3,
    Days31To60 = 4,
    Days60Plus = 5
}

public enum CreditSourceType
{
    OpeningBalance = 0,
    Sale = 1,
    Unscheduled = 2
}

public sealed record CreditSummary(
    decimal TotalOutstanding,
    decimal TodayDue,
    decimal Overdue,
    decimal DueNext7Days,
    decimal DueThisMonth,
    decimal Unscheduled,
    int CustomerCount);

public sealed record CreditAgingSummary(CreditAgingBucket Bucket, decimal Amount, int CustomerCount);

public sealed record CreditLimitWarning(
    int CustomerId,
    string CustomerName,
    decimal CurrentBalance,
    decimal CreditLimit,
    decimal OverBy);

public sealed record CreditObligationRow(
    int CustomerId,
    string CustomerName,
    string? BusinessName,
    string Phone,
    bool CustomerIsActive,
    decimal CreditLimit,
    long? SaleId,
    string Reference,
    DateTime SourceDate,
    DateTime? DueDate,
    decimal OutstandingAmount,
    int DaysOverdue,
    CreditStatus Status,
    CreditAgingBucket AgingBucket,
    CreditSourceType SourceType);

public sealed record CreditDashboard(
    DateTime AsOfDate,
    CreditSummary Summary,
    IReadOnlyList<CreditAgingSummary> Aging,
    IReadOnlyList<CreditObligationRow> Rows,
    IReadOnlyList<CreditLimitWarning> CreditLimitWarnings)
{
    public static CreditDashboard Empty(DateTime today) => new(
        today,
        new CreditSummary(0m, 0m, 0m, 0m, 0m, 0m, 0),
        [],
        [],
        []);
}
