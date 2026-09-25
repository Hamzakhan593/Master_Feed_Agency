using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Products;

[Authorize(Policy = AppPermissions.ViewProducts)]
public class HistoryModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly StockService _stockService;

    public HistoryModel(ApplicationDbContext db, StockService stockService)
    {
        _db = db;
        _stockService = stockService;
    }

    public Product? ProductItem { get; private set; }
    public decimal CurrentStock { get; private set; }
    public List<HistoryRow> Rows { get; private set; } = [];

    public sealed record HistoryRow(
        long Id,
        DateTime Date,
        StockTransactionType Type,
        decimal QuantityIn,
        decimal QuantityOut,
        decimal BalanceAfter,
        string Reason,
        string UserName,
        string? Reference,
        bool NegativeOverrideUsed);

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ProductItem = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (ProductItem is null)
        {
            return NotFound();
        }

        CurrentStock = await _stockService.GetCurrentStockAsync(id);

        var transactions = await _db.StockTransactions
            .AsNoTracking()
            .Where(x => x.ProductId == id)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .ToListAsync();

        var userIds = transactions
            .SelectMany(x => new[] { x.CreatedByUserId, x.ApprovedByUserId })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName);

        var running = 0m;
        var rows = new List<HistoryRow>();
        foreach (var entry in transactions)
        {
            running += entry.QuantityIn - entry.QuantityOut;
            var reference = string.IsNullOrWhiteSpace(entry.ReferenceType)
                ? null
                : string.IsNullOrWhiteSpace(entry.ReferenceId)
                    ? entry.ReferenceType
                    : $"{entry.ReferenceType} #{entry.ReferenceId}";

            rows.Add(new HistoryRow(
                entry.Id,
                entry.Date,
                entry.Type,
                entry.QuantityIn,
                entry.QuantityOut,
                running,
                entry.Reason,
                users.GetValueOrDefault(entry.CreatedByUserId, "Unknown user"),
                reference,
                entry.NegativeStockOverrideUsed));
        }

        Rows = rows.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToList();
        return Page();
    }

    public static string GetTypeLabel(StockTransactionType type) => type switch
    {
        StockTransactionType.Opening => "Shuru Ka Stock",
        StockTransactionType.AdjustmentIn => "Stock Add Kiya",
        StockTransactionType.AdjustmentOut => "Stock Kam Kiya",
        StockTransactionType.SaleIssue => "Sale Mein Diya",
        StockTransactionType.SaleReversal => "Sale Cancel Par Wapas",
        StockTransactionType.PurchaseIn => "Khareeda Hua Stock",
        StockTransactionType.OtherIn => "Doosra Stock Aaya",
        StockTransactionType.OtherOut => "Doosra Stock Gaya",
        _ => type.ToString()
    };
}
