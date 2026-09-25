using System.Data;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

public sealed class PaymentService
{
    private readonly ApplicationDbContext _db;
    public PaymentService(ApplicationDbContext db) => _db = db;

    public async Task<PaymentPostResult> ReceiveAsync(PaymentPostRequest request, string userId, CancellationToken ct = default)
    {
        if (request.CustomerId <= 0) return PaymentPostResult.Fail("Customer search karke select karein.");
        if (!Enum.IsDefined(request.Method)) return PaymentPostResult.Fail("Payment ka sahi tareeqa select karein.");
        if (Round(request.Amount) <= 0m) return PaymentPostResult.Fail("Payment ki raqam zero se zyada honi chahiye.");
        if (string.IsNullOrWhiteSpace(request.ClientRequestId)) return PaymentPostResult.Fail("Invalid payment request. Refresh and try again.");

        var requestId = request.ClientRequestId.Trim();
        var duplicate = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.ClientRequestId == requestId, ct);
        if (duplicate is not null) return PaymentPostResult.Success(duplicate.Id, duplicate.ReceiptNo, true);

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            duplicate = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.ClientRequestId == requestId, ct);
            if (duplicate is not null)
            {
                await tx.CommitAsync(ct);
                return PaymentPostResult.Success(duplicate.Id, duplicate.ReceiptNo, true);
            }

            var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, ct);
            if (customer is null) return await RollbackFail(tx, "Customer not found.", ct);
            if (!customer.IsActive) return await RollbackFail(tx, "Inactive customer cannot receive a new payment entry.", ct);

            var balance = await _db.CustomerLedgerEntries
                .Where(x => x.CustomerId == request.CustomerId)
                .SumAsync(x => (decimal?)(x.Debit - x.Credit), ct) ?? 0m;
            balance = Round(balance);

            if (balance <= 0m) return await RollbackFail(tx, "Is customer ka koi baqaya nahi hai.", ct);
            if (request.Amount > balance) return await RollbackFail(tx, $"Baqaya Rs. {balance:N2} hai. Payment is se zyada nahi ho sakti.", ct);

            var now = DateTime.UtcNow;
            var payment = new Payment
            {
                ReceiptNo = CreateReceiptNo(now),
                ClientRequestId = requestId,
                CustomerId = request.CustomerId,
                Amount = Round(request.Amount),
                PaymentDate = now,
                Method = request.Method,
                ReceivedByUserId = userId,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                Status = PaymentStatus.Posted,
                CreatedAt = now
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync(ct);

            _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
            {
                CustomerId = customer.Id,
                Date = now,
                Type = CustomerLedgerEntryType.Payment,
                Debit = 0m,
                Credit = payment.Amount,
                ReferenceType = "Payment",
                ReferenceId = payment.ReceiptNo,
                Description = $"Payment received - receipt {payment.ReceiptNo}",
                CreatedByUserId = userId
            });


            // Oldest credit invoices first. Any remainder naturally applies to opening/unmapped balance.
            var sales = await _db.Sales
                .Where(x => x.CustomerId == customer.Id && x.Status == SaleStatus.Posted && x.CreditAmount > 0m)
                .OrderBy(x => x.DueDate ?? DateTime.MaxValue)
                .ThenBy(x => x.SaleDate)
                .ThenBy(x => x.Id)
                .Select(x => new { x.Id, x.CreditAmount })
                .ToListAsync(ct);

            var saleIds = sales.Select(s => s.Id).ToArray();
            var priorAllocations = await _db.PaymentAllocations
                .Where(x => x.SaleId.HasValue && saleIds.Contains(x.SaleId.Value) && x.Payment.Status == PaymentStatus.Posted)
                .GroupBy(x => x.SaleId!.Value)
                .Select(g => new { SaleId = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToDictionaryAsync(x => x.SaleId, x => x.Amount, ct);

            var remaining = payment.Amount;
            foreach (var sale in sales)
            {
                if (remaining <= 0m) break;
                var open = Round(sale.CreditAmount - priorAllocations.GetValueOrDefault(sale.Id));
                if (open <= 0m) continue;
                var applied = Math.Min(open, remaining);
                _db.PaymentAllocations.Add(new PaymentAllocation { PaymentId = payment.Id, SaleId = sale.Id, Amount = Round(applied) });
                remaining = Round(remaining - applied);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return PaymentPostResult.Success(payment.Id, payment.ReceiptNo);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            var existing = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.ClientRequestId == requestId, ct);
            return existing is not null
                ? PaymentPostResult.Success(existing.Id, existing.ReceiptNo, true)
                : PaymentPostResult.Fail("Payment save nahi hui. Customer ka khata change nahi hua.");
        }
    }

    public async Task<PaymentPostResult> ReverseAsync(long id, string reason, string userId, CancellationToken ct = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason)) return PaymentPostResult.Fail("Payment cancel karne ki wajah likhein.");

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (payment is null) return await RollbackFail(tx, "Payment not found.", ct);
        if (payment.Status == PaymentStatus.Reversed) return await RollbackFail(tx, "Payment is already reversed.", ct);

        var now = DateTime.UtcNow;
        payment.Status = PaymentStatus.Reversed;
        payment.ReversedAt = now;
        payment.ReversedByUserId = userId;
        payment.ReversalReason = reason;

        _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
        {
            CustomerId = payment.CustomerId,
            Date = now,
            Type = CustomerLedgerEntryType.Reversal,
            Debit = payment.Amount,
            Credit = 0m,
            ReferenceType = "PaymentReversal",
            ReferenceId = payment.ReceiptNo,
            Description = $"Reversal of payment {payment.ReceiptNo}: {reason}",
            CreatedByUserId = userId
        });


        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return PaymentPostResult.Success(payment.Id, payment.ReceiptNo);
    }

    private static async Task<PaymentPostResult> RollbackFail(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string error, CancellationToken ct)
    {
        await tx.RollbackAsync(ct);
        return PaymentPostResult.Fail(error);
    }

    private static string CreateReceiptNo(DateTime now) => $"PAY-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public sealed record PaymentPostRequest(int CustomerId, decimal Amount, PaymentMethod Method, string? Notes, string ClientRequestId);
public sealed record PaymentPostResult(bool Succeeded, string? Error, long? PaymentId, string? ReceiptNo, bool DuplicateSubmission = false)
{
    public static PaymentPostResult Fail(string error) => new(false, error, null, null);
    public static PaymentPostResult Success(long id, string receipt, bool duplicate = false) => new(true, null, id, receipt, duplicate);
}
