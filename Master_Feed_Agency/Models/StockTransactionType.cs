namespace Master_Feed_Agency.Models;

public enum StockTransactionType
{
    Opening = 1,
    AdjustmentIn = 2,
    AdjustmentOut = 3,
    SaleIssue = 4,
    SaleReversal = 5,
    PurchaseIn = 6,
    OtherIn = 7,
    OtherOut = 8
}
