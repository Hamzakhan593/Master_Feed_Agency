using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Pages.Customers;

[Authorize(Policy = AppPermissions.ManageCustomers)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly CustomerService _customerService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(ApplicationDbContext db, CustomerService customerService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _customerService = customerService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public decimal OpeningBalance { get; private set; }

    public sealed class InputModel
    {
        public int Id { get; set; }

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
        [Display(Name = "Udhaar Ki Limit")]
        public decimal CreditLimit { get; set; }

        [Range(0, 3650)]
        [Display(Name = "Udhaar Ke Din")]
        public int DefaultCreditDays { get; set; }

        [StringLength(150)]
        [Display(Name = "Guarantor / reference name")]
        public string? GuarantorName { get; set; }

        [StringLength(30)]
        [Display(Name = "Guarantor / reference phone")]
        public string? GuarantorPhone { get; set; }

        [StringLength(500)]
        [Display(Name = "Delivery notes")]
        public string? DeliveryNotes { get; set; }

        [Display(Name = "Active customer")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = customer.Id,
            Name = customer.Name,
            BusinessName = customer.BusinessName,
            Phone = customer.Phone,
            CnicOrIdentifier = customer.CnicOrIdentifier,
            Area = customer.Area,
            Address = customer.Address,
            Notes = customer.Notes,
            CreditLimit = customer.CreditLimit,
            DefaultCreditDays = customer.DefaultCreditDays,
            GuarantorName = customer.GuarantorName,
            GuarantorPhone = customer.GuarantorPhone,
            DeliveryNotes = customer.DeliveryNotes,
            IsActive = customer.IsActive
        };

        await LoadOpeningBalanceAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOpeningBalanceAsync(Input.Id);
            return Page();
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var customer = new Customer
        {
            Id = Input.Id,
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
            DeliveryNotes = Input.DeliveryNotes,
            IsActive = Input.IsActive
        };

        var result = await _customerService.UpdateCustomerAsync(customer, userId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Customer could not be updated.");
            await LoadOpeningBalanceAsync(Input.Id);
            return Page();
        }

        TempData["SuccessMessage"] = $"Customer '{customer.Name}' updated successfully.";
        return RedirectToPage("Details", new { id = Input.Id });
    }

    private async Task LoadOpeningBalanceAsync(int customerId)
    {
        OpeningBalance = await _db.CustomerLedgerEntries
            .AsNoTracking()
            .Where(x => x.CustomerId == customerId && x.Type == CustomerLedgerEntryType.OpeningBalance)
            .SumAsync(x => (decimal?)(x.Debit - x.Credit)) ?? 0m;
    }
}
