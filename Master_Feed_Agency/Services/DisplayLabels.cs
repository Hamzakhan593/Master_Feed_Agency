using Master_Feed_Agency.Models;
namespace Master_Feed_Agency.Services;
public static class DisplayLabels
{
    public static string Khata(CustomerLedgerEntryType value, string? referenceType) => referenceType switch
    {
        "SaleCancellation" => "Bill Cancel Hua",
        "SalePaymentCancellation" => "Bill Ki Payment Wapas Hui",
        "PaymentReversal" => "Payment Cancel Hui",
        "SalePayment" => "Sale Par Payment Mili",
        _ => Khata(value)
    };
    public static string Khata(CustomerLedgerEntryType value) => value switch
    {
        CustomerLedgerEntryType.OpeningBalance => "Purana Baqaya",
        CustomerLedgerEntryType.Sale => "Sale Hui",
        CustomerLedgerEntryType.Payment => "Payment Mili",
        CustomerLedgerEntryType.Reversal => "Cancellation",
        _ => "Baqaya Ki Tabdeeli"
    };
}
