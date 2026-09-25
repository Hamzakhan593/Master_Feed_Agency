using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Master_Feed_Agency.Services;

public sealed class StockService
{
    private readonly ApplicationDbContext _db;

    public StockService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetCurrentStockAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _db.StockTransactions
            .Where(x => x.ProductId == productId)
            .SumAsync(x => (decimal?)(x.QuantityIn - x.QuantityOut), cancellationToken) ?? 0m;
    }

    public async Task<StockOperationResult> CreateProductAsync(
        Product product,
        decimal openingStock,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (openingStock < 0)
        {
            return StockOperationResult.Fail("Opening stock cannot be negative.");
        }

        product.Code = NormalizeCode(product.Code);
        product.Name = product.Name.Trim();
        product.Unit = product.Unit.Trim();
        product.CreatedAt = DateTime.UtcNow;
        product.CreatedByUserId = userId;

        if (await _db.Products.AnyAsync(x => x.Code == product.Code, cancellationToken))
        {
            return StockOperationResult.Fail($"Product code '{product.Code}' already exists.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync(cancellationToken);

            if (openingStock > 0)
            {
                _db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = product.Id,
                    Date = DateTime.UtcNow,
                    Type = StockTransactionType.Opening,
                    QuantityIn = openingStock,
                    QuantityOut = 0m,
                    Reason = "Opening stock",
                    CreatedByUserId = userId,
                    ApprovedByUserId = userId
                });

                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return StockOperationResult.Success(product.Id, openingStock);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StockOperationResult.Fail("The product could not be saved. Check that the product code is unique and try again.");
        }
    }

    public async Task<StockOperationResult> AdjustStockAsync(
        int productId,
        bool increase,
        decimal quantity,
        string reason,
        string userId,
        bool allowNegativeOverride,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return StockOperationResult.Fail("Adjustment quantity must be greater than zero.");
        }

        reason = reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return StockOperationResult.Fail("A reason is required for every stock adjustment.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var productExists = await _db.Products.AnyAsync(x => x.Id == productId, cancellationToken);
        if (!productExists)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StockOperationResult.Fail("Product not found.");
        }

        var currentStock = await GetCurrentStockAsync(productId, cancellationToken);
        var newBalance = increase ? currentStock + quantity : currentStock - quantity;
        var usesOverride = !increase && newBalance < 0m;

        if (usesOverride && !allowNegativeOverride)
        {
            await transaction.RollbackAsync(cancellationToken);
            return StockOperationResult.Fail($"This adjustment would make stock negative ({newBalance:0.###}). Only an Owner with negative-stock override permission can allow it.");
        }

        _db.StockTransactions.Add(new StockTransaction
        {
            ProductId = productId,
            Date = DateTime.UtcNow,
            Type = increase ? StockTransactionType.AdjustmentIn : StockTransactionType.AdjustmentOut,
            QuantityIn = increase ? quantity : 0m,
            QuantityOut = increase ? 0m : quantity,
            Reason = reason,
            CreatedByUserId = userId,
            ApprovedByUserId = userId,
            NegativeStockOverrideUsed = usesOverride
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return StockOperationResult.Success(productId, newBalance);
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}

public sealed record StockOperationResult(bool Succeeded, string? Error, int? ProductId, decimal? Balance)
{
    public static StockOperationResult Success(int productId, decimal balance) =>
        new(true, null, productId, balance);

    public static StockOperationResult Fail(string error) =>
        new(false, error, null, null);
}
