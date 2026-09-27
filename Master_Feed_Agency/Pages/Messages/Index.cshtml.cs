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
    public DateTime UpcomingDueDate { get; private set; }
    public OwnerDailySummary TodaySummary { get; private set; } = new(BusinessTime.Today, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<DueReminderCandidate> DueReminders { get; private set; } = [];
    public IReadOnlyList<WhatsAppMessageLog> RecentLogs { get; private set; } = [];
    public List<CustomerMessageOption> Customers { get; private set; } = [];
    public HashSet<int> SentDueCustomerIds { get; private set; } = [];

    public sealed record CustomerMessageOption(int Id, string Name, string Phone, string? BusinessName, decimal Balance);

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
            TempData["ErrorMessage"] = "Is customer ka is date par koi open due amount nahi mila.";
            return RedirectToPage();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _messaging.SendDueReminderAsync(candidate, automatic: false, userId, cancellationToken);
        SetResult(result);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSendAllDueAsync(DateTime dueDate, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var candidates = await _messaging.GetDueReminderCandidatesAsync(dueDate, cancellationToken);
        var sent = 0;
        var already = 0;
        var failed = 0;

        foreach (var candidate in candidates)
        {
            var result = await _messaging.SendDueReminderAsync(candidate, automatic: false, userId, cancellationToken);
            if (result.Succeeded && result.AlreadySent) already++;
            else if (result.Succeeded) sent++;
            else failed++;
        }

        TempData[failed > 0 ? "ErrorMessage" : "SuccessMessage"] =
            $"Due reminders: {sent} send, {already} pehle se sent, {failed} failed.";
        return RedirectToPage();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var daysBefore = Math.Max(0, Settings.DueReminderDaysBefore);
        UpcomingDueDate = BusinessTime.Today.AddDays(daysBefore);

        TodaySummary = await _messaging.GetOwnerDailySummaryAsync(BusinessTime.Today, cancellationToken);
        DueReminders = await _messaging.GetDueReminderCandidatesAsync(UpcomingDueDate, cancellationToken);
        RecentLogs = await _messaging.GetRecentLogsAsync(30, cancellationToken);

        var dueKeySuffix = $":{UpcomingDueDate:yyyyMMdd}";
        var sentKeys = RecentLogs
            .Where(x => x.Kind == WhatsAppMessageKind.DueReminder &&
                        x.Status == WhatsAppMessageStatus.Sent &&
                        x.DeduplicationKey.EndsWith(dueKeySuffix, StringComparison.Ordinal))
            .Select(x => x.CustomerId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToHashSet();
        SentDueCustomerIds = sentKeys;

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
            .Select(x => new CustomerMessageOption(x.Id, x.Name, x.Phone, x.BusinessName, balances.GetValueOrDefault(x.Id)))
            .ToList();
    }

    private void SetResult(WhatsAppActionResult result)
    {
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Message;
    }
}
