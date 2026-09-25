using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Sales;

[Authorize]
public class ReceiptModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public ReceiptModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public Sale Sale { get; private set; } = null!;
    public string CreatedByName { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(long id)
    {
        if (!User.HasPermission(AppPermissions.CreateSales) && !User.HasPermission(AppPermissions.ViewLedger))
        {
            return Forbid();
        }

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
        CreatedByName = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == sale.CreatedByUserId)
            .Select(x => x.FullName)
            .FirstOrDefaultAsync() ?? sale.CreatedByUserId;

        return Page();
    }
}
