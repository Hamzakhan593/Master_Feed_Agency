using System.ComponentModel.DataAnnotations;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Sales;

[Authorize(Policy = AppPermissions.CreateSales)]
public class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly SaleService _saleService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(ApplicationDbContext db, SaleService saleService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _saleService = saleService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public List<CustomerOption> Customers { get; private set; } = [];
    public List<ProductOption> Products { get; private set; } = [];

    public sealed class InputModel
    {
        [Required]
        public string ClientRequestId { get; set; } = string.Empty;

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Display(Name = "Sale type")]
        public SaleType SaleType { get; set; } = SaleType.Cash;

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal Discount { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Abhi Wasool Raqam")]
        public decimal PaidAtSale { get; set; }

        [Display(Name = "Payment method")]
        public PaymentMethod? PaymentMethod { get; set; } = global::Master_Feed_Agency.Models.PaymentMethod.Cash;

        [DataType(DataType.Date)]
        [Display(Name = "Payment Ki Tareekh")]
        public DateTime? DueDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "Stock se zyada sale ki ijazat dein")]
        public bool AllowNegativeStockOverride { get; set; }

        public List<LineInput> Items { get; set; } = [];
    }

    public sealed class LineInput
    {
        [Display(Name = "Product")]
        public int ProductId { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal Quantity { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        public decimal Rate { get; set; }
    }

    public sealed record CustomerOption(
        int Id,
        string Name,
        string Phone,
        string? BusinessName,
        decimal CurrentBalance,
        decimal CreditLimit,
        int DefaultCreditDays);

    public sealed record ProductOption(
        int Id,
        string Code,
        string Name,
        string Unit,
        decimal DefaultSalePrice,
        decimal CurrentStock);

    public async Task OnGetAsync(int? customerId)
    {
        Input.ClientRequestId = Guid.NewGuid().ToString("N");
        Input.CustomerId = customerId;
        Input.SaleType = SaleType.Cash;
        Input.PaymentMethod = PaymentMethod.Cash;
        Input.Items = [new LineInput { Quantity = 1m }];

        await LoadLookupsAsync();

        if (customerId.HasValue)
        {
            var customer = Customers.FirstOrDefault(x => x.Id == customerId.Value);
            if (customer is not null)
            {
                Input.DueDate = BusinessTime.Today.AddDays(customer.DefaultCreditDays);
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var postedItems = Input.Items
            .Where(x => x.ProductId != 0 || x.Quantity != 0m || x.Rate != 0m)
            .Select(x => new SaleLineRequest(x.ProductId, x.Quantity, x.Rate))
            .ToList();

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return Page();
        }

        var request = new SalePostRequest(
            Input.ClientRequestId,
            Input.CustomerId,
            Input.SaleType,
            Input.Discount,
            Input.PaidAtSale,
            Input.PaymentMethod,
            Input.DueDate,
            Input.Notes,
            postedItems);

        var canOverrideNegative = Input.AllowNegativeStockOverride && User.HasPermission(AppPermissions.OverrideNegativeStock);
        var result = await _saleService.PostSaleAsync(request, userId, canOverrideNegative);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Sale could not be saved.");
            await LoadLookupsAsync();
            return Page();
        }

        TempData["SuccessMessage"] = result.DuplicateSubmission
            ? $"Yeh sale pehle se saved hai. Bill: {result.InvoiceNo}."
            : $"Sale save ho gayi. Bill: {result.InvoiceNo}.";

        return RedirectToPage("Details", new { id = result.SaleId });
    }

    private async Task LoadLookupsAsync()
    {
        var customers = await _db.Customers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Phone,
                x.BusinessName,
                x.CreditLimit,
                x.DefaultCreditDays
            })
            .ToListAsync();

        var customerIds = customers.Select(x => x.Id).ToArray();
        var customerBalances = customerIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await _db.CustomerLedgerEntries
                .AsNoTracking()
                .Where(x => customerIds.Contains(x.CustomerId))
                .GroupBy(x => x.CustomerId)
                .Select(g => new { CustomerId = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Balance);

        Customers = customers
            .Select(x => new CustomerOption(
                x.Id,
                x.Name,
                x.Phone,
                x.BusinessName,
                customerBalances.GetValueOrDefault(x.Id),
                x.CreditLimit,
                x.DefaultCreditDays))
            .ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Code)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Unit,
                x.DefaultSalePrice
            })
            .ToListAsync();

        var productIds = products.Select(x => x.Id).ToArray();
        var stockBalances = productIds.Length == 0
            ? new Dictionary<int, decimal>()
            : await _db.StockTransactions
                .AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId))
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Balance = g.Sum(x => x.QuantityIn - x.QuantityOut) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Balance);

        Products = products
            .Select(x => new ProductOption(
                x.Id,
                x.Code,
                x.Name,
                x.Unit,
                x.DefaultSalePrice,
                stockBalances.GetValueOrDefault(x.Id)))
            .ToList();
    }
}
