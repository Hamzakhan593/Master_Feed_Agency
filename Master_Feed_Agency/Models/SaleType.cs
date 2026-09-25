using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Models;

public enum SaleType
{
    [Display(Name = "Cash")]
    Cash = 1,

    [Display(Name = "Udhaar — poori raqam baad mein")]
    Credit = 2,

    [Display(Name = "Kuch Payment + Baqi Udhaar")]
    PartPaymentCredit = 3
}
