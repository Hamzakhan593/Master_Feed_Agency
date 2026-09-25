using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Pages.Customers;

[Authorize(Policy = AppPermissions.ManageCustomers)]
public class CreateModel : PageModel
{
    private readonly CustomerService _customerService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(CustomerService customerService, UserManager<ApplicationUser> userManager)
    {
        _customerService = customerService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required, StringLength(150)]
        [Display(Name = "Customer name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(150)]
        [Display(Name = "Business name (optional)")]
        public string? BusinessName { get; set; }

        [Required, StringLength(30)]
        public string Phone { get; set; } = string.Empty;

        [StringLength(25)]
        [Display(Name = "CNIC / Identifier (optional)")]
        public string? CnicOrIdentifier { get; set; }

        [StringLength(120)]
        public string? Area { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Purana Baqaya")]
        public decimal OpeningBalance { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Udhaar Ki Limit")]
        public decimal CreditLimit { get; set; }

        [Range(0, 3650)]
        [Display(Name = "Udhaar Ke Din")]
        public int DefaultCreditDays { get; set; } = 30;

        [StringLength(150)]
        [Display(Name = "Guarantor / reference name")]
        public string? GuarantorName { get; set; }

        [StringLength(30)]
        [Display(Name = "Guarantor / reference phone")]
        public string? GuarantorPhone { get; set; }

        [StringLength(500)]
        [Display(Name = "Delivery notes")]
        public string? DeliveryNotes { get; set; }
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var customer = new Customer
        {
            Name = Input.Name,
            BusinessName = Input.BusinessName,
            Phone = Input.Phone,
            CnicOrIdentifier = Input.CnicOrIdentifier,
            Area = Input.Area,
            Address = Input.Address,
            Notes = Input.Notes,
            CreditLimit = Input.CreditLimit,
            DefaultCreditDays = Input.DefaultCreditDays,
            GuarantorName = Input.GuarantorName,
            GuarantorPhone = Input.GuarantorPhone,
            DeliveryNotes = Input.DeliveryNotes
        };

        var result = await _customerService.CreateCustomerAsync(customer, Input.OpeningBalance, userId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Customer could not be created.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Customer '{customer.Name}' created successfully.";
        return RedirectToPage("Details", new { id = result.CustomerId });
    }
}
