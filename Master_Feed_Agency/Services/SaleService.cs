using System.Data;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

public sealed class SaleService
{
    private readonly ApplicationDbContext _db;

    public SaleService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SalePostResult> PostSaleAsync(
        SalePostRequest request,
        string userId,
        bool allowNegativeStockOverride,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return SalePostResult.Fail(validationError);
        }

        var requestId = request.ClientRequestId.Trim();
        var alreadyPosted = await _db.Sales
            .AsNoTracking()
            .Where(x => x.ClientRequestId == requestId)
            .Select(x => new { x.Id, x.InvoiceNo })
            .FirstOrDefaultAsync(cancellationToken);

        if (alreadyPosted is not null)
        {
            return SalePostResult.Success(alreadyPosted.Id, alreadyPosted.InvoiceNo, duplicateSubmission: true);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            // Re-check inside the transaction so a browser double-click cannot post twice.
            alreadyPosted = await _db.Sales
                .AsNoTracking()
                .Where(x => x.ClientRequestId == requestId)
                .Select(x => new { x.Id, x.InvoiceNo })
                .FirstOrDefaultAsync(cancellationToken);

            if (alreadyPosted is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SalePostResult.Success(alreadyPosted.Id, alreadyPosted.InvoiceNo, duplicateSubmission: true);
            }

            var productIds = request.Items.Select(x => x.ProductId).ToArray();
            if (productIds.Distinct().Count() != productIds.Length)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Ek product do baar add hua hai. Uski quantity ek hi row mein likhein.");
            }

            var products = await _db.Products
                .Where(x => productIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            if (products.Count != productIds.Length)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("One or more selected products no longer exist.");
            }

            var inactiveProduct = products.Values.FirstOrDefault(x => !x.IsActive);
            if (inactiveProduct is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail($"Product '{inactiveProduct.Name}' is inactive and cannot be sold.");
            }

            Customer? customer = null;
            if (request.CustomerId.HasValue)
            {
                customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId.Value, cancellationToken);
                if (customer is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalePostResult.Fail("Customer not found.");
                }

                if (!customer.IsActive)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalePostResult.Fail("The selected customer is inactive and cannot receive a new sale.");
                }
            }

            var calculatedLines = request.Items
                .Select(x => new CalculatedSaleLine(
                    x.ProductId,
                    x.Quantity,
                    x.Rate,
                    RoundMoney(x.Quantity * x.Rate)))
                .ToList();

            var grossTotal = RoundMoney(calculatedLines.Sum(x => x.LineTotal));
            if (request.Discount > grossTotal)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Discount products ke total se zyada nahi ho sakta.");
            }

            var discount = RoundMoney(request.Discount);
            var netTotal = RoundMoney(grossTotal - discount);
            if (netTotal <= 0m)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Net sale total must be greater than zero.");
            }

            var paymentResult = ResolvePayment(request, netTotal);
            if (!paymentResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail(paymentResult.Error!);
            }

            var paidAtSale = paymentResult.PaidAtSale;
            var creditAmount = RoundMoney(netTotal - paidAtSale);

            if (creditAmount > 0m && customer is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Udhaar sale ke liye customer search karke select karein.");
            }

            if (creditAmount > 0m && !request.DueDate.HasValue)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Baqi udhaar ki payment ki tareekh select karein.");
            }

            if (creditAmount == 0m && request.SaleType != SaleType.Cash)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SalePostResult.Fail("Use Cash sale when the full invoice is paid at the counter.");
            }

            // Module 6 credit-limit control. Zero means no limit has been configured.
            // This runs inside the serializable sale transaction so simultaneous sales cannot silently bypass the limit.
            if (creditAmount > 0m && customer is not null && customer.CreditLimit > 0m)
            {
                var currentBalance = await _db.CustomerLedgerEntries
                    .Where(x => x.CustomerId == customer.Id)
                    .SumAsync(x => (decimal?)(x.Debit - x.Credit), cancellationToken) ?? 0m;

                var projectedBalance = RoundMoney(currentBalance + creditAmount);
                if (projectedBalance > customer.CreditLimit)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return SalePostResult.Fail(
                        $"Credit limit exceeded for {customer.Name}. Current outstanding: Rs. {currentBalance:N2}, " +
                        $"new credit: Rs. {creditAmount:N2}, projected: Rs. {projectedBalance:N2}, " +
                        $"limit: Rs. {customer.CreditLimit:N2}.");
                }
            }

            var stockBalances = await _db.StockTransactions
                .Where(x => productIds.Contains(x.ProductId))
                .GroupBy(x => x.ProductId)
                .Select(g => new { ProductId = g.Key, Balance = g.Sum(x => x.QuantityIn - x.QuantityOut) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Balance, cancellationToken);

            var negativeOverrideProducts = new HashSet<int>();
            foreach (var line in calculatedLines)
            {
                var currentStock = stockBalances.GetValueOrDefault(line.ProductId);
                var newBalance = currentStock - line.Quantity;
                if (newBalance < 0m)
                {
                    if (!allowNegativeStockOverride)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        var product = products[line.ProductId];
                        return SalePostResult.Fail($"{product.Name} ka available stock {currentStock:0.###} {product.Unit} hai. Quantity is se kam ya barabar karein; aap ne {line.Quantity:0.###} likhi hai.");
                    }

                    negativeOverrideProducts.Add(line.ProductId);
                }
            }

            var now = DateTime.UtcNow;
            var invoiceNo = CreateInvoiceNumber(now);
            var sale = new Sale
            {
                InvoiceNo = invoiceNo,
                ClientRequestId = requestId,
                CustomerId = customer?.Id,
                SaleDate = now,
                SaleType = request.SaleType,
                GrossTotal = grossTotal,
                Discount = discount,
                NetTotal = netTotal,
                PaidAtSale = paidAtSale,
                CreditAmount = creditAmount,
                DueDate = creditAmount > 0m ? request.DueDate!.Value.Date : null,
                InitialPaymentMethod = paidAtSale > 0m ? request.PaymentMethod : null,
                Notes = NullIfWhiteSpace(request.Notes),
                Status = SaleStatus.Posted,
                CreatedByUserId = userId,
                CreatedAt = now
            };

            _db.Sales.Add(sale);
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var line in calculatedLines)
            {
                _db.SaleItems.Add(new SaleItem
                {
                    SaleId = sale.Id,
                    ProductId = line.ProductId,
                    Quantity = line.Quantity,
                    Rate = RoundMoney(line.Rate),
                    LineTotal = line.LineTotal
                });

                _db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = line.ProductId,
                    Date = now,
                    Type = StockTransactionType.SaleIssue,
                    QuantityIn = 0m,
                    QuantityOut = line.Quantity,
                    ReferenceType = "Sale",
                    ReferenceId = sale.InvoiceNo,
                    Reason = $"Stock issued against invoice {sale.InvoiceNo}",
                    CreatedByUserId = userId,
                    ApprovedByUserId = negativeOverrideProducts.Contains(line.ProductId) ? userId : null,
                    NegativeStockOverrideUsed = negativeOverrideProducts.Contains(line.ProductId)
                });
            }

            if (customer is not null)
            {
                _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                {
                    CustomerId = customer.Id,
                    Date = now,
                    Type = CustomerLedgerEntryType.Sale,
                    Debit = netTotal,
                    Credit = 0m,
                    ReferenceType = "Sale",
                    ReferenceId = sale.InvoiceNo,
                    Description = $"Sale invoice {sale.InvoiceNo}",
                    CreatedByUserId = userId
                });

                if (paidAtSale > 0m)
                {
                    _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                    {
                        CustomerId = customer.Id,
                        Date = now,
                        Type = CustomerLedgerEntryType.Payment,
                        Debit = 0m,
                        Credit = paidAtSale,
                        ReferenceType = "SalePayment",
                        ReferenceId = sale.InvoiceNo,
                        Description = $"Payment received at sale for invoice {sale.InvoiceNo}",
                        CreatedByUserId = userId
                    });
                }
            }


            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return SalePostResult.Success(sale.Id, sale.InvoiceNo);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);

            var duplicate = await _db.Sales
                .AsNoTracking()
                .Where(x => x.ClientRequestId == requestId)
                .Select(x => new { x.Id, x.InvoiceNo })
                .FirstOrDefaultAsync(cancellationToken);

            if (duplicate is not null)
            {
                return SalePostResult.Success(duplicate.Id, duplicate.InvoiceNo, duplicateSubmission: true);
            }

            return SalePostResult.Fail("Sale save nahi hui. Hisaab aur stock change nahi hua. Dobara koshish karein.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<SaleCancelResult> CancelSaleAsync(
        long saleId,
        string reason,
        string userId,
        CancellationToken cancellationToken = default)
    {
        reason = reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return SaleCancelResult.Fail("Cancellation reason is required.");
        }

        if (reason.Length > 500)
        {
            return SaleCancelResult.Fail("Cancellation reason cannot exceed 500 characters.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var sale = await _db.Sales
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.Id == saleId, cancellationToken);

            if (sale is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SaleCancelResult.Fail("Sale not found.");
            }

            if (sale.Status == SaleStatus.Cancelled)
            {
                await transaction.CommitAsync(cancellationToken);
                return SaleCancelResult.Success(sale.InvoiceNo, alreadyCancelled: true);
            }

            if (await _db.PaymentAllocations.AnyAsync(x => x.SaleId == saleId && x.Payment.Status == PaymentStatus.Posted, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return SaleCancelResult.Fail("Is bill par payment wasool ho chuki hai. Pehle customer ke khate se us payment ko cancel karein.");
            }
            var now = DateTime.UtcNow;

            foreach (var item in sale.Items)
            {
                _db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = item.ProductId,
                    Date = now,
                    Type = StockTransactionType.SaleReversal,
                    QuantityIn = item.Quantity,
                    QuantityOut = 0m,
                    ReferenceType = "SaleCancellation",
                    ReferenceId = sale.InvoiceNo,
                    Reason = $"Stock restored because invoice {sale.InvoiceNo} was cancelled: {reason}",
                    CreatedByUserId = userId,
                    ApprovedByUserId = userId,
                    NegativeStockOverrideUsed = false
                });
            }

            if (sale.CustomerId.HasValue)
            {
                _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                {
                    CustomerId = sale.CustomerId.Value,
                    Date = now,
                    Type = CustomerLedgerEntryType.Reversal,
                    Debit = 0m,
                    Credit = sale.NetTotal,
                    ReferenceType = "SaleCancellation",
                    ReferenceId = sale.InvoiceNo,
                    Description = $"Reversal of cancelled invoice {sale.InvoiceNo}",
                    CreatedByUserId = userId
                });

                if (sale.PaidAtSale > 0m)
                {
                    _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                    {
                        CustomerId = sale.CustomerId.Value,
                        Date = now,
                        Type = CustomerLedgerEntryType.Reversal,
                        Debit = sale.PaidAtSale,
                        Credit = 0m,
                        ReferenceType = "SalePaymentCancellation",
                        ReferenceId = sale.InvoiceNo,
                        Description = $"Reversal of payment received at cancelled invoice {sale.InvoiceNo}",
                        CreatedByUserId = userId
                    });
                }
            }


            sale.Status = SaleStatus.Cancelled;
            sale.CancelledAt = now;
            sale.CancelledByUserId = userId;
            sale.CancelReason = reason;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SaleCancelResult.Success(sale.InvoiceNo);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return SaleCancelResult.Fail("The invoice could not be cancelled. No reversal was committed.");
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string? ValidateRequest(SalePostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientRequestId))
        {
            return "Missing sale request identifier. Refresh the page and try again.";
        }

        if (request.PaymentMethod.HasValue && !Enum.IsDefined(request.PaymentMethod.Value))
            return "Payment ka sahi tareeqa select karein.";

        if (request.Items.Count == 0)
        {
            return "Add at least one product to the sale.";
        }

        if (request.Items.Any(x => x.ProductId <= 0))
        {
            return "Select a product on every sale line.";
        }

        if (request.Items.Any(x => x.Quantity <= 0m))
        {
            return "Quantity must be greater than zero on every sale line.";
        }

        if (request.Items.Any(x => x.Rate <= 0m))
        {
            return "Rate must be greater than zero on every sale line.";
        }

        if (request.Discount < 0m)
        {
            return "Discount cannot be negative.";
        }

        if (request.SaleType != SaleType.Cash && request.SaleType != SaleType.Credit && request.SaleType != SaleType.PartPaymentCredit)
        {
            return "Select a valid sale type.";
        }

        return null;
    }

    private static PaymentResolution ResolvePayment(SalePostRequest request, decimal netTotal)
    {
        switch (request.SaleType)
        {
            case SaleType.Cash:
                if (!request.PaymentMethod.HasValue)
                {
                    return PaymentResolution.Fail("Payment method is required for a cash sale.");
                }

                return PaymentResolution.Success(netTotal);

            case SaleType.Credit:
                return PaymentResolution.Success(0m);

            case SaleType.PartPaymentCredit:
                if (!request.PaymentMethod.HasValue)
                {
                    return PaymentResolution.Fail("Payment method is required for the amount paid at sale.");
                }

                var paid = RoundMoney(request.PaidAtSale);
                if (paid <= 0m)
                {
                    return PaymentResolution.Fail("Abhi mili hui payment ki raqam likhein.");
                }

                if (paid >= netTotal)
                {
                    return PaymentResolution.Fail("Kuch payment total bill se kam honi chahiye. Poori payment mil gayi hai to Cash select karein.");
                }

                return PaymentResolution.Success(paid);

            default:
                return PaymentResolution.Fail("Select a valid sale type.");
        }
    }

    private static decimal RoundMoney(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string CreateInvoiceNumber(DateTime utcNow) =>
        $"MFA-{utcNow:yyMMdd}-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record CalculatedSaleLine(int ProductId, decimal Quantity, decimal Rate, decimal LineTotal);

    private sealed record PaymentResolution(bool Succeeded, decimal PaidAtSale, string? Error)
    {
        public static PaymentResolution Success(decimal paidAtSale) => new(true, paidAtSale, null);
        public static PaymentResolution Fail(string error) => new(false, 0m, error);
    }
}

public sealed record SaleLineRequest(int ProductId, decimal Quantity, decimal Rate);

public sealed record SalePostRequest(
    string ClientRequestId,
    int? CustomerId,
    SaleType SaleType,
    decimal Discount,
    decimal PaidAtSale,
    PaymentMethod? PaymentMethod,
    DateTime? DueDate,
    string? Notes,
    IReadOnlyList<SaleLineRequest> Items);

public sealed record SalePostResult(bool Succeeded, string? Error, long? SaleId, string? InvoiceNo, bool DuplicateSubmission)
{
    public static SalePostResult Success(long saleId, string invoiceNo, bool duplicateSubmission = false) =>
        new(true, null, saleId, invoiceNo, duplicateSubmission);

    public static SalePostResult Fail(string error) => new(false, error, null, null, false);
}

public sealed record SaleCancelResult(bool Succeeded, string? Error, string? InvoiceNo, bool AlreadyCancelled)
{
    public static SaleCancelResult Success(string invoiceNo, bool alreadyCancelled = false) =>
        new(true, null, invoiceNo, alreadyCancelled);

    public static SaleCancelResult Fail(string error) => new(false, error, null, false);
}
