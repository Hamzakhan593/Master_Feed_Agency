using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class Sale
{
    public long Id { get; set; }

    [Required, StringLength(50)]
    public string InvoiceNo { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string ClientRequestId { get; set; } = string.Empty;

    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public DateTime SaleDate { get; set; } = DateTime.UtcNow;

    public SaleType SaleType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAtSale { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; }

    public DateTime? DueDate { get; set; }

    public PaymentMethod? InitialPaymentMethod { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Posted;

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    [StringLength(450)]
    public string? CancelledByUserId { get; set; }

    [StringLength(500)]
    public string? CancelReason { get; set; }

    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
