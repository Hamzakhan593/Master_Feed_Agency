using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class Payment
{
    public long Id { get; set; }

    [Required, StringLength(50)]
    public string ReceiptNo { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string ClientRequestId { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [Required, StringLength(450)]
    public string ReceivedByUserId { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Notes { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Posted;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReversedAt { get; set; }

    [StringLength(450)]
    public string? ReversedByUserId { get; set; }

    [StringLength(500)]
    public string? ReversalReason { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
