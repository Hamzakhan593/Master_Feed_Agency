using Microsoft.Extensions.Options;

namespace Master_Feed_Agency.Services;

public sealed class WhatsAppAutomationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<WhatsAppOptions> _options;
    private readonly ILogger<WhatsAppAutomationService> _logger;

    public WhatsAppAutomationService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<WhatsAppOptions> options,
        ILogger<WhatsAppAutomationService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay lets the web host finish starting before background DB/API work begins.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WhatsApp automation cycle failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.IsConfigured) return;

        var localNow = BusinessTime.ToLocal(DateTime.UtcNow);
        using var scope = _scopeFactory.CreateScope();
        var messaging = scope.ServiceProvider.GetRequiredService<WhatsAppMessagingService>();

        if (options.DueReminderEnabled && localNow.Hour >= ClampHour(options.DueReminderHour))
        {
            // Normal reminder is sent N days before the due date. We also check the dates between
            // today and that target date so a reminder is not permanently missed if the app/server
            // was temporarily offline. Deduplication inside WhatsAppMessagingService prevents repeats.
            var daysBefore = Math.Max(0, options.DueReminderDaysBefore);
            for (var offset = 0; offset <= daysBefore; offset++)
            {
                var dueDate = localNow.Date.AddDays(offset);
                await messaging.SendDueRemindersForDateAsync(
                    dueDate,
                    automatic: true,
                    cancellationToken: cancellationToken);
            }
        }

        if (options.DailyOwnerReportEnabled && localNow.Hour >= ClampHour(options.DailyOwnerReportHour))
        {
            await messaging.SendOwnerDailyReportAsync(localNow.Date, automatic: true, cancellationToken: cancellationToken);
        }
    }

    private static int ClampHour(int hour) => Math.Clamp(hour, 0, 23);
}
