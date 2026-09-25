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

namespace Master_Feed_Agency.Pages.Products;

[Authorize(Policy = AppPermissions.ManageProducts)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly StockService _stockService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(ApplicationDbContext db, StockService stockService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _stockService = stockService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public decimal CurrentStock { get; private set; }

    public sealed class InputModel
    {
        [Required]
        public int Id { get; set; }

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
        [Display(Name = "Low-stock threshold")]
        public decimal LowStockThreshold { get; set; }

        [Display(Name = "Product is active")]
        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Id = product.Id,
            Code = product.Code,
            Name = product.Name,
            Unit = product.Unit,
            PurchaseCost = product.PurchaseCost,
            DefaultSalePrice = product.DefaultSalePrice,
            LowStockThreshold = product.LowStockThreshold,
            IsActive = product.IsActive
        };

        CurrentStock = await _stockService.GetCurrentStockAsync(product.Id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            CurrentStock = await _stockService.GetCurrentStockAsync(Input.Id);
            return Page();
        }

        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == Input.Id);
        if (product is null)
        {
            return NotFound();
        }

        var normalizedCode = StockService.NormalizeCode(Input.Code);
        var duplicateCode = await _db.Products.AnyAsync(x => x.Id != product.Id && x.Code == normalizedCode);
        if (duplicateCode)
        {
            ModelState.AddModelError("Input.Code", $"Product code '{normalizedCode}' already exists.");
            CurrentStock = await _stockService.GetCurrentStockAsync(Input.Id);
            return Page();
        }

        product.Code = normalizedCode;
        product.Name = Input.Name.Trim();
        product.Unit = Input.Unit.Trim();
        product.PurchaseCost = Input.PurchaseCost;
        product.DefaultSalePrice = Input.DefaultSalePrice;
        product.LowStockThreshold = Input.LowStockThreshold;
        product.IsActive = Input.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedByUserId = _userManager.GetUserId(User);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Input.Code", "The product could not be saved. The code may already be in use.");
            CurrentStock = await _stockService.GetCurrentStockAsync(Input.Id);
            return Page();
        }

        TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully. Stock quantity was not changed.";
        return RedirectToPage("Index");
    }
}
