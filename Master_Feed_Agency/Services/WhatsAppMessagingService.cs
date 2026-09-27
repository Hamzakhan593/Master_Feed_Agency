using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Master_Feed_Agency.Services;

public sealed record WhatsAppActionResult(bool Succeeded, bool AlreadySent, string Message, long? LogId = null)
{
    public static WhatsAppActionResult Success(string message, long? logId = null) => new(true, false, message, logId);
    public static WhatsAppActionResult Duplicate(string message, long? logId = null) => new(true, true, message, logId);
    public static WhatsAppActionResult Fail(string message, long? logId = null) => new(false, false, message, logId);
}

public sealed record DueReminderCandidate(
    int CustomerId,
    string CustomerName,
    string Phone,
    DateTime DueDate,
    decimal Amount,
    int InvoiceCount,
    string InvoiceReference);

public sealed record OwnerDailySummary(
    DateTime BusinessDate,
    int BillCount,
    decimal TotalSales,
    decimal PaidAtSale,
    decimal NewCredit,
    decimal CustomerPayments,
    decimal TotalOutstanding);

public sealed class WhatsAppMessagingService
{
    private readonly ApplicationDbContext _db;
    private readonly WhatsAppCloudGateway _gateway;
    private readonly IOptionsMonitor<WhatsAppOptions> _options;
    private readonly ILogger<WhatsAppMessagingService> _logger;

    public WhatsAppMessagingService(
        ApplicationDbContext db,
        WhatsAppCloudGateway gateway,
        IOptionsMonitor<WhatsAppOptions> options,
        ILogger<WhatsAppMessagingService> logger)
    {
        _db = db;
        _gateway = gateway;
        _options = options;
        _logger = logger;
    }

    public WhatsAppOptions Options => _options.CurrentValue;
    public bool IsConfigured => _gateway.IsConfigured;

    public async Task<WhatsAppActionResult> SendPaymentReceiptAsync(
        long paymentId,
        string? createdByUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments
            .AsNoTracking()
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null) return WhatsAppActionResult.Fail("Payment nahi mili.");
        if (payment.Status != PaymentStatus.Posted) return WhatsAppActionResult.Fail("Cancelled payment ka message nahi bheja ja sakta.");

        var remaining = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => x.CustomerId == payment.CustomerId)
            .SumAsync(x => (decimal?)(x.Debit - x.Credit), cancellationToken) ?? 0m;
        remaining = Math.Max(0m, Math.Round(remaining, 2, MidpointRounding.AwayFromZero));

        var balanceText = remaining <= 0m
            ? "Aap ka khata clear ho gaya hai."
            : $"Aap ka baqaya Rs. {remaining:N2} rehta hai.";

        var preview =
            $"{payment.Customer.Name}: Rs. {payment.Amount:N2} payment receive hui. Receipt {payment.ReceiptNo}. {balanceText}";

        return await SendLoggedTemplateAsync(
            new WhatsAppLogRequest(
                WhatsAppMessageKind.PaymentReceived,
                payment.Customer.Phone,
                Options.PaymentReceivedTemplate,
                preview,
                $"payment:{payment.Id}:receipt",
                IsAutomatic: false,
                CustomerId: payment.CustomerId,
                PaymentId: payment.Id,
                SaleId: null,
                CreatedByUserId: createdByUserId,
                Parameters:
                [
                    payment.Customer.Name,
                    $"{payment.Amount:N2}",
                    payment.ReceiptNo,
                    balanceText
                ]),
            cancellationToken);
    }

    public async Task<OwnerDailySummary> GetOwnerDailySummaryAsync(
        DateTime businessDate,
        CancellationToken cancellationToken = default)
    {
        var startUtc = BusinessTime.ToUtcStart(businessDate);
        var endUtc = BusinessTime.ToUtcEndExclusive(businessDate);

        var sales = await _db.Sales
            .AsNoTracking()
            .Where(x => x.Status == SaleStatus.Posted && x.SaleDate >= startUtc && x.SaleDate < endUtc)
            .Select(x => new { x.NetTotal, x.PaidAtSale, x.CreditAmount })
            .ToListAsync(cancellationToken);

        var customerPayments = await _db.Payments
            .AsNoTracking()
            .Where(x => x.Status == PaymentStatus.Posted && x.PaymentDate >= startUtc && x.PaymentDate < endUtc)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var balances = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .GroupBy(x => x.CustomerId)
            .Select(g => g.Sum(x => x.Debit - x.Credit))
            .ToListAsync(cancellationToken);

        var totalOutstanding = balances.Where(x => x > 0m).Sum();

        return new OwnerDailySummary(
            businessDate.Date,
            sales.Count,
            Round(sales.Sum(x => x.NetTotal)),
            Round(sales.Sum(x => x.PaidAtSale)),
            Round(sales.Sum(x => x.CreditAmount)),
            Round(customerPayments),
            Round(totalOutstanding));
    }

    public async Task<WhatsAppActionResult> SendOwnerDailyReportAsync(
        DateTime businessDate,
        bool automatic,
        string? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var options = Options;
        if (string.IsNullOrWhiteSpace(options.OwnerPhone))
            return WhatsAppActionResult.Fail("Owner ka WhatsApp number configured nahi hai.");

        var summary = await GetOwnerDailySummaryAsync(businessDate, cancellationToken);
        var dateText = summary.BusinessDate.ToString("dd MMM yyyy");
        var preview =
            $"{dateText}: Sales Rs. {summary.TotalSales:N2}, counter received Rs. {summary.PaidAtSale:N2}, " +
            $"naya udhaar Rs. {summary.NewCredit:N2}, customer wasooli Rs. {summary.CustomerPayments:N2}, " +
            $"total baqaya Rs. {summary.TotalOutstanding:N2}.";

        var key = automatic
            ? $"owner-daily:{summary.BusinessDate:yyyyMMdd}"
            : $"owner-manual:{summary.BusinessDate:yyyyMMdd}:{Guid.NewGuid():N}";

        return await SendLoggedTemplateAsync(
            new WhatsAppLogRequest(
                WhatsAppMessageKind.OwnerDailyReport,
                options.OwnerPhone,
                options.OwnerDailyReportTemplate,
                preview,
                key,
                automatic,
                CustomerId: null,
                PaymentId: null,
                SaleId: null,
                CreatedByUserId: createdByUserId,
                Parameters:
                [
                    dateText,
                    summary.BillCount.ToString(),
                    $"{summary.TotalSales:N2}",
                    $"{summary.PaidAtSale:N2}",
                    $"{summary.NewCredit:N2}",
                    $"{summary.CustomerPayments:N2}",
                    $"{summary.TotalOutstanding:N2}"
                ]),
            cancellationToken);
    }

    public async Task<IReadOnlyList<DueReminderCandidate>> GetDueReminderCandidatesAsync(
        DateTime dueDate,
        CancellationToken cancellationToken = default)
    {
        var targetDate = dueDate.Date;

        var sales = await _db.Sales
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x =>
                x.Status == SaleStatus.Posted &&
                x.CustomerId.HasValue &&
                x.Customer != null &&
                x.Customer.IsActive &&
                x.CreditAmount > 0m &&
                x.DueDate.HasValue &&
                x.DueDate.Value.Date == targetDate)
            .Select(x => new
            {
                x.Id,
                x.CustomerId,
                CustomerName = x.Customer!.Name,
                Phone = x.Customer.Phone,
                x.InvoiceNo,
                x.CreditAmount
            })
            .ToListAsync(cancellationToken);

        if (sales.Count == 0) return [];

        var saleIds = sales.Select(x => x.Id).ToArray();
        var allocations = await _db.PaymentAllocations
            .AsNoTracking()
            .Where(x => x.SaleId.HasValue && saleIds.Contains(x.SaleId.Value) && x.Payment.Status == PaymentStatus.Posted)
            .GroupBy(x => x.SaleId!.Value)
            .Select(g => new { SaleId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SaleId, x => x.Amount, cancellationToken);

        return sales
            .Select(x => new
            {
                Sale = x,
                Open = Round(x.CreditAmount - allocations.GetValueOrDefault(x.Id))
            })
            .Where(x => x.Open > 0m)
            .GroupBy(x => new { CustomerId = x.Sale.CustomerId!.Value, x.Sale.CustomerName, x.Sale.Phone })
            .Select(g =>
            {
                var invoices = g.Select(x => x.Sale.InvoiceNo).OrderBy(x => x).ToArray();
                var reference = invoices.Length == 1 ? invoices[0] : $"{invoices[0]} +{invoices.Length - 1}";
                return new DueReminderCandidate(
                    g.Key.CustomerId,
                    g.Key.CustomerName,
                    g.Key.Phone,
                    targetDate,
                    Round(g.Sum(x => x.Open)),
                    invoices.Length,
                    reference);
            })
            .OrderBy(x => x.CustomerName)
            .ToList();
    }

    public async Task<WhatsAppActionResult> SendDueReminderAsync(
        DueReminderCandidate candidate,
        bool automatic,
        string? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var options = Options;
        var dateText = candidate.DueDate.ToString("dd MMM yyyy");
        var preview =
            $"{candidate.CustomerName}: Rs. {candidate.Amount:N2} ki payment {dateText} ko due hai. Ref: {candidate.InvoiceReference}.";

        return await SendLoggedTemplateAsync(
            new WhatsAppLogRequest(
                WhatsAppMessageKind.DueReminder,
                candidate.Phone,
                options.DueReminderTemplate,
                preview,
                $"due:{candidate.CustomerId}:{candidate.DueDate:yyyyMMdd}",
                automatic,
                CustomerId: candidate.CustomerId,
                PaymentId: null,
                SaleId: null,
                CreatedByUserId: createdByUserId,
                Parameters:
                [
                    candidate.CustomerName,
                    $"{candidate.Amount:N2}",
                    dateText,
                    candidate.InvoiceReference
                ]),
            cancellationToken);
    }

    public async Task<int> SendDueRemindersForDateAsync(
        DateTime dueDate,
        bool automatic,
        string? createdByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var candidates = await GetDueReminderCandidatesAsync(dueDate, cancellationToken);
        var sent = 0;

        foreach (var candidate in candidates)
        {
            var result = await SendDueReminderAsync(candidate, automatic, createdByUserId, cancellationToken);
            if (result.Succeeded && !result.AlreadySent) sent++;
        }

        return sent;
    }

    public async Task<IReadOnlyList<WhatsAppMessageLog>> GetRecentLogsAsync(
        int count = 25,
        CancellationToken cancellationToken = default)
    {
        return await _db.WhatsAppMessages
            .AsNoTracking()
            .Include(x => x.Customer)
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(count, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> WasDueReminderSentAsync(
        int customerId,
        DateTime dueDate,
        CancellationToken cancellationToken = default)
    {
        var key = $"due:{customerId}:{dueDate:yyyyMMdd}";
        return await _db.WhatsAppMessages
            .AsNoTracking()
            .AnyAsync(x => x.DeduplicationKey == key && x.Status == WhatsAppMessageStatus.Sent, cancellationToken);
    }

    private async Task<WhatsAppActionResult> SendLoggedTemplateAsync(
        WhatsAppLogRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _db.WhatsAppMessages
            .FirstOrDefaultAsync(x => x.DeduplicationKey == request.DeduplicationKey, cancellationToken);

        if (existing is not null && existing.Status == WhatsAppMessageStatus.Sent)
            return WhatsAppActionResult.Duplicate("Yeh WhatsApp message pehle hi send ho chuka hai.", existing.Id);

        var recipient = WhatsAppCloudGateway.NormalizePhone(request.Recipient);
        if (string.IsNullOrWhiteSpace(recipient))
            return WhatsAppActionResult.Fail("Recipient ka WhatsApp number sahi nahi hai.");

        var log = existing ?? new WhatsAppMessageLog
        {
            Kind = request.Kind,
            Recipient = recipient,
            TemplateName = request.TemplateName,
            MessagePreview = request.Preview,
            IsAutomatic = request.IsAutomatic,
            DeduplicationKey = request.DeduplicationKey,
            CustomerId = request.CustomerId,
            PaymentId = request.PaymentId,
            SaleId = request.SaleId,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            Status = WhatsAppMessageStatus.Pending
        };

        if (existing is null)
        {
            _db.WhatsAppMessages.Add(log);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _db.Entry(log).State = EntityState.Detached;
                existing = await _db.WhatsAppMessages
                    .FirstOrDefaultAsync(x => x.DeduplicationKey == request.DeduplicationKey, cancellationToken);
                if (existing?.Status == WhatsAppMessageStatus.Sent)
                    return WhatsAppActionResult.Duplicate("Yeh WhatsApp message pehle hi send ho chuka hai.", existing.Id);
                if (existing is null) throw;
                log = existing;
            }
        }
        else
        {
            log.TemplateName = request.TemplateName;
            log.MessagePreview = request.Preview;
            log.Recipient = recipient;
            log.ErrorMessage = null;
            log.Status = WhatsAppMessageStatus.Pending;
        }

        log.AttemptedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var result = await _gateway.SendTemplateAsync(recipient, request.TemplateName, request.Parameters, cancellationToken);
        if (result.Succeeded)
        {
            log.Status = WhatsAppMessageStatus.Sent;
            log.ProviderReference = result.ProviderReference;
            log.ErrorMessage = null;
            log.SentAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return WhatsAppActionResult.Success("WhatsApp message send ho gaya.", log.Id);
        }

        log.Status = WhatsAppMessageStatus.Failed;
        log.ErrorMessage = Truncate(result.Error ?? "WhatsApp message send nahi ho saka.", 1000);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("WhatsApp message {LogId} failed: {Error}", log.Id, log.ErrorMessage);
        return WhatsAppActionResult.Fail(log.ErrorMessage, log.Id);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed record WhatsAppLogRequest(
        WhatsAppMessageKind Kind,
        string Recipient,
        string TemplateName,
        string Preview,
        string DeduplicationKey,
        bool IsAutomatic,
        int? CustomerId,
        long? PaymentId,
        long? SaleId,
        string? CreatedByUserId,
        IReadOnlyList<string> Parameters);
}
