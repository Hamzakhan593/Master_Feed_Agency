using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Models;

public enum PaymentMethod
{
    [Display(Name = "Cash")]
    Cash = 1,

    [Display(Name = "Bank Transfer")]
    BankTransfer = 2,

    [Display(Name = "Cheque")]
    Cheque = 3,

    [Display(Name = "Other")]
    Other = 4
}
