namespace Master_Feed_Agency.Services;

public sealed class WhatsAppOptions
{
    public bool Enabled { get; set; }
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = "en_US";
    public string OwnerPhone { get; set; } = string.Empty;
    public string AgencyName { get; set; } = "Master Feed Agency";

    public bool DailyOwnerReportEnabled { get; set; } = true;
    public int DailyOwnerReportHour { get; set; } = 22;

    public bool DueReminderEnabled { get; set; } = true;
    public int DueReminderHour { get; set; } = 10;
    public int DueReminderDaysBefore { get; set; } = 2;

    public string OwnerDailyReportTemplate { get; set; } = "owner_daily_report";
    public string DueReminderTemplate { get; set; } = "customer_due_reminder";
    public string PaymentReceivedTemplate { get; set; } = "payment_received";

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(PhoneNumberId) &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(ApiVersion);
}
