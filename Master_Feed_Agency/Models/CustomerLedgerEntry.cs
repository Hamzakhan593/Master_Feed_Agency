using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class CustomerLedgerEntry
{
    public long Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public CustomerLedgerEntryType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; }

    [StringLength(50)]
    public string? ReferenceType { get; set; }

    [StringLength(100)]
    public string? ReferenceId { get; set; }

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
}
