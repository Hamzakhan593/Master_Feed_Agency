using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

/// <summary>
/// Single query/service boundary for customer khata balances and statements.
/// Financial screens should use this service rather than maintaining their own balance formula.
/// </summary>
public sealed class LedgerService
{
    private readonly ApplicationDbContext _db;

    public LedgerService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetCurrentBalanceAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .SumAsync(x => (decimal?)(x.Debit - x.Credit), cancellationToken) ?? 0m;
    }

    public async Task<Dictionary<int, decimal>> GetBalancesAsync(
        IEnumerable<int> customerIds,
        CancellationToken cancellationToken = default)
    {
        var ids = customerIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return new Dictionary<int, decimal>();
        }

        return await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => ids.Contains(x.CustomerId))
            .GroupBy(x => x.CustomerId)
            .Select(g => new { CustomerId = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
            .ToDictionaryAsync(x => x.CustomerId, x => x.Balance, cancellationToken);
    }

    public async Task<CustomerAccountSummary?> GetAccountSummaryAsync(
        int customerId,
        int recentCount = 10,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Customers
            .AsNoTracking()
            .AnyAsync(x => x.Id == customerId, cancellationToken);

        if (!exists)
        {
            return null;
        }

        var entries = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var openingBalance = entries
            .Where(x => x.Type == CustomerLedgerEntryType.OpeningBalance)
            .Sum(x => x.Debit - x.Credit);

        var totalSales = entries
            .Where(x => x.Type == CustomerLedgerEntryType.Sale)
            .Sum(x => x.Debit)
            - entries.Where(x =>
                    x.Type == CustomerLedgerEntryType.Reversal &&
                    string.Equals(x.ReferenceType, "SaleCancellation", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Credit);

        var totalPayments = entries
            .Where(x => x.Type == CustomerLedgerEntryType.Payment)
            .Sum(x => x.Credit)
            - entries.Where(x =>
                    x.Type == CustomerLedgerEntryType.Reversal &&
                    !string.IsNullOrWhiteSpace(x.ReferenceType) &&
                    x.ReferenceType.Contains("Payment", StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.Debit);

        var currentBalance = entries.Sum(x => x.Debit - x.Credit);
        var recentEntries = entries
            .OrderByDescending(x => x.Date)
            .ThenByDescending(x => x.Id)
            .Take(Math.Max(0, recentCount))
            .ToList();

        return new CustomerAccountSummary(
            openingBalance,
            totalSales,
            totalPayments,
            currentBalance,
            recentEntries);
    }

    public async Task<CustomerStatement?> GetStatementAsync(
        int customerId,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

        if (customer is null)
        {
            return null;
        }

        var fromInclusive = from.HasValue ? BusinessTime.ToUtcStart(from.Value) : (DateTime?)null;
        var toExclusive = to.HasValue ? BusinessTime.ToUtcEndExclusive(to.Value) : (DateTime?)null;

        var currentBalance = await GetCurrentBalanceAsync(customerId, cancellationToken);

        decimal balanceBroughtForward = 0m;
        if (fromInclusive.HasValue)
        {
            balanceBroughtForward = await _db.CustomerLedgerEntries
                .AsNoTracking()
                .Where(x => x.CustomerId == customerId && x.Date < fromInclusive.Value)
                .SumAsync(x => (decimal?)(x.Debit - x.Credit), cancellationToken) ?? 0m;
        }

        var query = _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId);

        if (fromInclusive.HasValue)
        {
            query = query.Where(x => x.Date >= fromInclusive.Value);
        }

        if (toExclusive.HasValue)
        {
            query = query.Where(x => x.Date < toExclusive.Value);
        }

        var entries = await query
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var invoiceReferences = entries
            .Where(IsSaleRelatedReference)
            .Select(x => x.ReferenceId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var saleIdsByInvoice = invoiceReferences.Length == 0
            ? new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
            : (await _db.Sales
                    .AsNoTracking()
                    .Where(x => invoiceReferences.Contains(x.InvoiceNo))
                    .Select(x => new { x.InvoiceNo, x.Id })
                    .ToListAsync(cancellationToken))
                .ToDictionary(x => x.InvoiceNo, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var paymentReferences = entries.Where(x => x.ReferenceType == "Payment" || x.ReferenceType == "PaymentReversal")
            .Select(x => x.ReferenceId).Where(x => x != null).ToArray();
        var paymentIds = await _db.Payments.AsNoTracking().Where(x => paymentReferences.Contains(x.ReceiptNo))
            .ToDictionaryAsync(x => x.ReceiptNo, x => x.Id, cancellationToken);
        var runningBalance = balanceBroughtForward;
        var rows = new List<CustomerStatementRow>(entries.Count);

        foreach (var entry in entries)
        {
            runningBalance += entry.Debit - entry.Credit;
            long? saleId = null;
            if (!string.IsNullOrWhiteSpace(entry.ReferenceId) &&
                saleIdsByInvoice.TryGetValue(entry.ReferenceId, out var foundSaleId))
            {
                saleId = foundSaleId;
            }

            rows.Add(new CustomerStatementRow(
                entry.Id,
                entry.Date,
                entry.Type,
                entry.Description,
                entry.Debit,
                entry.Credit,
                runningBalance,
                entry.ReferenceType,
                entry.ReferenceId,
                saleId,
                entry.ReferenceId != null && paymentIds.TryGetValue(entry.ReferenceId, out var paymentId) ? paymentId : null));
        }

        return new CustomerStatement(
            customer,
            from?.Date,
            to?.Date,
            balanceBroughtForward,
            rows.Sum(x => x.Debit),
            rows.Sum(x => x.Credit),
            runningBalance,
            currentBalance,
            rows);
    }

    private static bool IsSaleRelatedReference(CustomerLedgerEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.ReferenceType) || string.IsNullOrWhiteSpace(entry.ReferenceId))
        {
            return false;
        }

        return entry.ReferenceType.Equals("Sale", StringComparison.OrdinalIgnoreCase)
               || entry.ReferenceType.Equals("SalePayment", StringComparison.OrdinalIgnoreCase)
               || entry.ReferenceType.Equals("SaleCancellation", StringComparison.OrdinalIgnoreCase)
               || entry.ReferenceType.Equals("SalePaymentCancellation", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record CustomerAccountSummary(
    decimal OpeningBalance,
    decimal TotalSales,
    decimal TotalPayments,
    decimal CurrentBalance,
    IReadOnlyList<CustomerLedgerEntry> RecentEntries);

public sealed record CustomerStatement(
    Customer Customer,
    DateTime? From,
    DateTime? To,
    decimal BalanceBroughtForward,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingBalance,
    decimal CurrentBalance,
    IReadOnlyList<CustomerStatementRow> Rows);

public sealed record CustomerStatementRow(
    long Id,
    DateTime Date,
    CustomerLedgerEntryType Type,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    string? ReferenceType,
    string? ReferenceId,
    long? SaleId,
    long? PaymentId);
