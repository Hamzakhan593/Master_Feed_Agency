using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class Customer
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    public string? BusinessName { get; set; }

    [Required, StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [StringLength(25)]
    public string? CnicOrIdentifier { get; set; }

    [StringLength(120)]
    public string? Area { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditLimit { get; set; }

    public int DefaultCreditDays { get; set; } = 30;

    [StringLength(150)]
    public string? GuarantorName { get; set; }

    [StringLength(30)]
    public string? GuarantorPhone { get; set; }

    [StringLength(500)]
    public string? DeliveryNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }

    public ICollection<CustomerLedgerEntry> LedgerEntries { get; set; } = new List<CustomerLedgerEntry>();
}
