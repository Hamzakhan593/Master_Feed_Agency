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

[Authorize(Policy = AppPermissions.AdjustStock)]
public class AdjustModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly StockService _stockService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdjustModel(ApplicationDbContext db, StockService stockService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _stockService = stockService;
        _userManager = userManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Product? ProductItem { get; private set; }
    public decimal CurrentStock { get; private set; }
    public bool CanOverrideNegative => User.HasPermission(AppPermissions.OverrideNegativeStock);

    public sealed class InputModel
    {
        [Required]
        public int ProductId { get; set; }

        [Required, RegularExpression("^(IN|OUT)$")]
        public string Direction { get; set; } = "IN";

        [Range(typeof(decimal), "0.001", "999999999999")]
        public decimal Quantity { get; set; }

        [Required, StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "Allow negative stock for this adjustment")]
        public bool AllowNegativeOverride { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Input.ProductId = id;
        if (!await LoadAsync(id))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.AllowNegativeOverride && !CanOverrideNegative)
        {
            ModelState.AddModelError(nameof(Input.AllowNegativeOverride), "You do not have permission to override negative stock protection.");
        }

        if (!await LoadAsync(Input.ProductId))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var increase = string.Equals(Input.Direction, "IN", StringComparison.OrdinalIgnoreCase);
        var result = await _stockService.AdjustStockAsync(
            Input.ProductId,
            increase,
            Input.Quantity,
            Input.Reason,
            userId,
            Input.AllowNegativeOverride && CanOverrideNegative);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Stock adjustment failed.");
            CurrentStock = await _stockService.GetCurrentStockAsync(Input.ProductId);
            return Page();
        }

        TempData["SuccessMessage"] = $"Stock adjusted successfully. New balance: {result.Balance:0.###} {ProductItem!.Unit}.";
        return RedirectToPage("Index");
    }

    private async Task<bool> LoadAsync(int productId)
    {
        ProductItem = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == productId);
        if (ProductItem is null)
        {
            return false;
        }

        CurrentStock = await _stockService.GetCurrentStockAsync(productId);
        return true;
    }
}
