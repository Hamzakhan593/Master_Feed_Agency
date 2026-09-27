using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Models;

public enum WhatsAppMessageKind
{
    OwnerDailyReport = 1,
    DueReminder = 2,
    PaymentReceived = 3
}

public enum WhatsAppMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2
}

public sealed class WhatsAppMessageLog
{
    public long Id { get; set; }

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }

    public long? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public WhatsAppMessageKind Kind { get; set; }

    [Required, StringLength(30)]
    public string Recipient { get; set; } = string.Empty;

    [StringLength(150)]
    public string? TemplateName { get; set; }

    [Required, StringLength(2000)]
    public string MessagePreview { get; set; } = string.Empty;

    public WhatsAppMessageStatus Status { get; set; } = WhatsAppMessageStatus.Pending;

    [StringLength(200)]
    public string? ProviderReference { get; set; }

    [StringLength(1000)]
    public string? ErrorMessage { get; set; }

    public bool IsAutomatic { get; set; }

    [Required, StringLength(180)]
    public string DeduplicationKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAt { get; set; }
    public DateTime? SentAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }
}
