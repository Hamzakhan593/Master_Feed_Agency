using System.ComponentModel.DataAnnotations.Schema;

namespace Master_Feed_Agency.Models;

public class PaymentAllocation
{
    public long Id { get; set; }
    public long PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;

    public long? SaleId { get; set; }
    public Sale? Sale { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
}
