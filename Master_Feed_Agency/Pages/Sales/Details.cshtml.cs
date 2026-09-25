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

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly SaleService _saleService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DetailsModel(ApplicationDbContext db, SaleService saleService, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _saleService = saleService;
        _userManager = userManager;
    }

    public Sale Sale { get; private set; } = null!;
    public string CreatedByName { get; private set; } = string.Empty;
    public string? CancelledByName { get; private set; }

    [BindProperty]
    [Required, StringLength(500)]
    [Display(Name = "Cancellation reason")]
    public string CancelReason { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!CanViewSale())
        {
            return Forbid();
        }

        return await LoadAsync(id);
    }

    public async Task<IActionResult> OnPostCancelAsync(long id)
    {
        if (!User.HasPermission(AppPermissions.CancelSales))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync(id);
        }

        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var result = await _saleService.CancelSaleAsync(id, CancelReason, userId);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.Error ?? "Invoice could not be cancelled.";
        }
        else if (result.AlreadyCancelled)
        {
            TempData["SuccessMessage"] = $"Invoice {result.InvoiceNo} was already cancelled. No duplicate reversal was created.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Invoice {result.InvoiceNo} cancelled. Stock, customer ledger and cash effects were reversed.";
        }

        return RedirectToPage(new { id });
    }

    private bool CanViewSale() =>
        User.HasPermission(AppPermissions.CreateSales) ||
        User.HasPermission(AppPermissions.ViewLedger) ||
        User.HasPermission(AppPermissions.CancelSales);

    private async Task<IActionResult> LoadAsync(long id)
    {
        var sale = await _db.Sales
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (sale is null)
        {
            return NotFound();
        }

        Sale = sale;

        var userIds = new[] { sale.CreatedByUserId, sale.CancelledByUserId }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToArray();

        var names = await _db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName);

        CreatedByName = names.GetValueOrDefault(sale.CreatedByUserId, sale.CreatedByUserId);
        if (!string.IsNullOrWhiteSpace(sale.CancelledByUserId))
        {
            CancelledByName = names.GetValueOrDefault(sale.CancelledByUserId, sale.CancelledByUserId);
        }

        return Page();
    }
}
