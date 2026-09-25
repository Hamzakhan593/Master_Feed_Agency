using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class Product
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string Unit { get; set; } = "Bag";

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchaseCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DefaultSalePrice { get; set; }

    [Column(TypeName = "decimal(18,3)")]
    public decimal LowStockThreshold { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }

    public ICollection<StockTransaction> StockTransactions { get; set; } = new List<StockTransaction>();
}
