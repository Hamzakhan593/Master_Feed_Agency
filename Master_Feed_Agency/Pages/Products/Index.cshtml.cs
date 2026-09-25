using Master_Feed_Agency.Data;
using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Products;

[Authorize(Policy = AppPermissions.ViewProducts)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "active";

    [BindProperty(SupportsGet = true)]
    public bool LowStockOnly { get; set; }

    public List<ProductRow> Products { get; private set; } = [];

    public sealed record ProductRow(
        int Id,
        string Code,
        string Name,
        string Unit,
        decimal? PurchaseCost,
        decimal DefaultSalePrice,
        decimal LowStockThreshold,
        decimal CurrentStock,
        bool IsActive)
    {
        public bool IsLowStock => CurrentStock <= LowStockThreshold;
    }

    public async Task OnGetAsync()
    {
        Status = string.IsNullOrWhiteSpace(Status) ? "active" : Status;
        var query = _db.Products.AsNoTracking();

        var normalizedSearch = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x => x.Code.Contains(normalizedSearch) || x.Name.Contains(normalizedSearch));
        }

        if (string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(Status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var products = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Code)
            .ToListAsync();

        var ids = products.Select(x => x.Id).ToArray();
        var balances = ids.Length == 0
            ? new Dictionary<int, decimal>()
            : await _db.StockTransactions
                .AsNoTracking()
                .Where(x => ids.Contains(x.ProductId))
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Balance = g.Sum(x => x.QuantityIn - x.QuantityOut) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Balance);

        Products = products
            .Select(x => new ProductRow(
                x.Id,
                x.Code,
                x.Name,
                x.Unit,
                x.PurchaseCost,
                x.DefaultSalePrice,
                x.LowStockThreshold,
                balances.GetValueOrDefault(x.Id),
                x.IsActive))
            .Where(x => !LowStockOnly || x.IsLowStock)
            .ToList();
    }
}
