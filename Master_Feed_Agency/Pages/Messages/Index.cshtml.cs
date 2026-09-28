using System.Security.Claims;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Messages;

[Authorize(Policy = AppPermissions.ManageMessages)]
public sealed class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly WhatsAppMessagingService _messaging;

    public IndexModel(ApplicationDbContext db, WhatsAppMessagingService messaging)
    {
        _db = db;
        _messaging = messaging;
    }

    public WhatsAppOptions Settings => _messaging.Options;
    public bool IsConfigured => _messaging.IsConfigured;
    public IReadOnlyList<ReminderScheduleRow> ReminderSchedule { get; private set; } = [];
    public IReadOnlyList<WhatsAppMessageLog> RecentLogs { get; private set; } = [];
    public List<CustomerMessageOption> Customers { get; private set; } = [];

    public sealed record CustomerMessageOption(int Id, string Name, string Phone, string? BusinessName, decimal Balance);

    public sealed record ReminderScheduleRow(
        int CustomerId,
        string CustomerName,
        string Phone,
        DateTime DueDate,
        DateTime MessageSendAt,
        decimal Amount,
        int InvoiceCount,
        string InvoiceReference,
        string State,
        bool IsSent,
        bool IsAutomaticSent,
        DateTime? SentAt,
        string? LastError,
        bool CanSendNow);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSendOwnerNowAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _messaging.SendOwnerDailyReportAsync(BusinessTime.Today, automatic: false, userId, cancellationToken);
        SetResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendDueAsync(int customerId, DateTime dueDate, CancellationToken cancellationToken)
    {
        var candidates = await _messaging.GetDueReminderCandidatesAsync(dueDate, cancellationToken);
        var candidate = candidates.FirstOrDefault(x => x.CustomerId == customerId);
        if (candidate is null)
        {
            TempData["ErrorMessage"] = "Is customer ka is due date par koi open amount nahi mila.";
            return RedirectToPage();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _messaging.SendDueReminderAsync(candidate, automatic: false, userId, cancellationToken);
        SetResult(result);
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        RecentLogs = await _messaging.GetRecentLogsAsync(15, cancellationToken);
        ReminderSchedule = await BuildReminderScheduleAsync(cancellationToken);
        await LoadCustomersAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ReminderScheduleRow>> BuildReminderScheduleAsync(CancellationToken cancellationToken)
    {
        var sales = await _db.Sales
            .AsNoTracking()
            .Where(x =>
                x.Status == SaleStatus.Posted &&
                x.CustomerId.HasValue &&
                x.Customer != null &&
                x.Customer.IsActive &&
                x.CreditAmount > 0m &&
                x.DueDate.HasValue)
            .Select(x => new
            {
                x.Id,
                CustomerId = x.CustomerId!.Value,
                CustomerName = x.Customer!.Name,
                Phone = x.Customer.Phone,
                x.InvoiceNo,
                x.CreditAmount,
                DueDate = x.DueDate!.Value
            })
            .ToListAsync(cancellationToken);

        if (sales.Count == 0)
            return [];

        var saleIds = sales.Select(x => x.Id).ToArray();
        var allocations = await _db.PaymentAllocations
            .AsNoTracking()
            .Where(x => x.SaleId.HasValue && saleIds.Contains(x.SaleId.Value) && x.Payment.Status == PaymentStatus.Posted)
            .GroupBy(x => x.SaleId!.Value)
            .Select(g => new { SaleId = g.Key, Amount = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.SaleId, x => x.Amount, cancellationToken);

        var dueLogs = await _db.WhatsAppMessages
            .AsNoTracking()
            .Where(x => x.Kind == WhatsAppMessageKind.DueReminder)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var latestByKey = dueLogs
            .GroupBy(x => x.DeduplicationKey)
            .ToDictionary(g => g.Key, g => g.First());

        var sentByKey = dueLogs
            .Where(x => x.Status == WhatsAppMessageStatus.Sent)
            .GroupBy(x => x.DeduplicationKey)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SentAt ?? x.CreatedAt).First());

        var daysBefore = Math.Max(0, Settings.DueReminderDaysBefore);
        var sendHour = Math.Clamp(Settings.DueReminderHour, 0, 23);
        var today = BusinessTime.Today;

        var groups = sales
            .Select(x => new
            {
                Sale = x,
                Open = Round(x.CreditAmount - allocations.GetValueOrDefault(x.Id))
            })
            .Where(x => x.Open > 0m)
            .GroupBy(x => new
            {
                x.Sale.CustomerId,
                x.Sale.CustomerName,
                x.Sale.Phone,
                DueDate = x.Sale.DueDate.Date
            });

        var rows = new List<ReminderScheduleRow>();

        foreach (var group in groups)
        {
            var dueDate = group.Key.DueDate;
            var messageSendAt = dueDate.AddDays(-daysBefore).AddHours(sendHour);
            var invoices = group.Select(x => x.Sale.InvoiceNo).OrderBy(x => x).ToArray();
            var reference = invoices.Length == 1 ? invoices[0] : $"{invoices[0]} +{invoices.Length - 1}";
            var amount = Round(group.Sum(x => x.Open));
            var key = $"due:{group.Key.CustomerId}:{dueDate:yyyyMMdd}";

            sentByKey.TryGetValue(key, out var sentLog);
            latestByKey.TryGetValue(key, out var latestLog);

            var isSent = sentLog is not null;
            var phoneMissing = string.IsNullOrWhiteSpace(group.Key.Phone);
            var state = GetReminderState(
                isSent,
                sentLog?.IsAutomatic == true,
                latestLog,
                phoneMissing,
                messageSendAt.Date,
                dueDate,
                today);

            rows.Add(new ReminderScheduleRow(
                group.Key.CustomerId,
                group.Key.CustomerName,
                group.Key.Phone,
                dueDate,
                messageSendAt,
                amount,
                invoices.Length,
                reference,
                state,
                isSent,
                sentLog?.IsAutomatic == true,
                sentLog?.SentAt.HasValue == true ? BusinessTime.ToLocal(sentLog.SentAt.Value) : null,
                latestLog?.Status == WhatsAppMessageStatus.Failed ? latestLog.ErrorMessage : null,
                IsConfigured && !isSent && !phoneMissing));
        }

        return rows
            .OrderBy(x => x.IsSent ? 1 : 0)
            .ThenBy(x => x.MessageSendAt)
            .ThenBy(x => x.DueDate)
            .ThenBy(x => x.CustomerName)
            .ToList();
    }

    private string GetReminderState(
        bool isSent,
        bool automaticSent,
        WhatsAppMessageLog? latestLog,
        bool phoneMissing,
        DateTime sendDate,
        DateTime dueDate,
        DateTime today)
    {
        if (isSent)
            return automaticSent ? "Auto Sent" : "Sent";

        if (latestLog?.Status == WhatsAppMessageStatus.Failed)
            return "Failed";

        if (phoneMissing)
            return "Phone Missing";

        if (!Settings.DueReminderEnabled)
            return "Paused";

        if (!IsConfigured)
            return "Setup Required";

        if (sendDate > today)
            return "Scheduled";

        if (sendDate == today)
            return "Aaj Send Hoga";

        if (dueDate >= today)
            return "Pending";

        return "Due Date Guzar Gayi";
    }

    private async Task LoadCustomersAsync(CancellationToken cancellationToken)
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Phone, x.BusinessName })
            .ToListAsync(cancellationToken);

        var ids = customers.Select(x => x.Id).ToArray();
        var balances = ids.Length == 0
            ? new Dictionary<int, decimal>()
            : await _db.CustomerLedgerEntries
                .AsNoTracking()
                .Where(x => ids.Contains(x.CustomerId))
                .GroupBy(x => x.CustomerId)
                .Select(g => new { CustomerId = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Balance, cancellationToken);

        Customers = customers
            .Select(x => new CustomerMessageOption(x.Id, x.Name, x.Phone, x.BusinessName, Round(balances.GetValueOrDefault(x.Id))))
            .ToList();
    }

    private void SetResult(WhatsAppActionResult result)
    {
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
