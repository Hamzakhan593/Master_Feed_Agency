using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class StockTransaction
{
    public long Id { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public DateTime Date { get; set; } = DateTime.UtcNow;

    public StockTransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityIn { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal QuantityOut { get; set; }

    [StringLength(50)]
    public string? ReferenceType { get; set; }

    [StringLength(100)]
    public string? ReferenceId { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [StringLength(450)]
    public string? ApprovedByUserId { get; set; }

    public bool NegativeStockOverrideUsed { get; set; }
}
