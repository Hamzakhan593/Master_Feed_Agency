using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace Master_Feed_Agency.Pages.Products;

[Authorize(Policy = AppPermissions.ManageProducts)]
public class CreateModel : PageModel
{
    private readonly StockService _stockService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CreateModel(StockService stockService, UserManager<ApplicationUser> userManager)
    {
        _stockService = stockService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public sealed class InputModel
    {
        [Required, StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string Unit { get; set; } = "Bag";

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Purchase cost (optional)")]
        public decimal? PurchaseCost { get; set; }

        [Range(typeof(decimal), "0", "9999999999999999")]
        [Display(Name = "Default sale price")]
        public decimal DefaultSalePrice { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        [Display(Name = "Opening stock")]
        public decimal OpeningStock { get; set; }

        [Range(typeof(decimal), "0", "999999999999")]
        [Display(Name = "Low-stock threshold")]
        public decimal LowStockThreshold { get; set; }
    }

    public void OnGet()
    {
    }

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

        var product = new Product
        {
            Code = Input.Code,
            Name = Input.Name,
            Unit = Input.Unit,
            PurchaseCost = Input.PurchaseCost,
            DefaultSalePrice = Input.DefaultSalePrice,
            LowStockThreshold = Input.LowStockThreshold,
            IsActive = true
        };

        var result = await _stockService.CreateProductAsync(product, Input.OpeningStock, userId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Product could not be created.");
            return Page();
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' created with opening stock {Input.OpeningStock:0.###} {product.Unit}.";
        return RedirectToPage("Index");
    }
}
