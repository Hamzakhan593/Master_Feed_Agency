using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Services;

public sealed class CustomerService
{
    private readonly ApplicationDbContext _db;
    private readonly LedgerService _ledgerService;

    public CustomerService(ApplicationDbContext db, LedgerService ledgerService)
    {
        _db = db;
        _ledgerService = ledgerService;
    }

    public async Task<CustomerOperationResult> CreateCustomerAsync(
        Customer customer,
        decimal openingBalance,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (openingBalance < 0m)
        {
            return CustomerOperationResult.Fail("Opening balance cannot be negative.");
        }

        if (customer.CreditLimit < 0m)
        {
            return CustomerOperationResult.Fail("Credit limit cannot be negative.");
        }

        if (customer.DefaultCreditDays < 0)
        {
            return CustomerOperationResult.Fail("Default credit days cannot be negative.");
        }

        Normalize(customer);
        customer.CreatedAt = DateTime.UtcNow;
        customer.CreatedByUserId = userId;
        customer.IsActive = true;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(cancellationToken);

            if (openingBalance > 0m)
            {
                _db.CustomerLedgerEntries.Add(new CustomerLedgerEntry
                {
                    CustomerId = customer.Id,
                    Date = DateTime.UtcNow,
                    Type = CustomerLedgerEntryType.OpeningBalance,
                    Debit = openingBalance,
                    Credit = 0m,
                    ReferenceType = "CustomerSetup",
                    ReferenceId = customer.Id.ToString(),
                    Description = "Opening balance entered during customer setup",
                    CreatedByUserId = userId
                });

                await _db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return CustomerOperationResult.Success(customer.Id);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return CustomerOperationResult.Fail("The customer could not be saved. Please review the details and try again.");
        }
    }

    public async Task<CustomerOperationResult> UpdateCustomerAsync(
        Customer customer,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (customer.CreditLimit < 0m)
        {
            return CustomerOperationResult.Fail("Credit limit cannot be negative.");
        }

        if (customer.DefaultCreditDays < 0)
        {
            return CustomerOperationResult.Fail("Default credit days cannot be negative.");
        }

        var existing = await _db.Customers.FirstOrDefaultAsync(x => x.Id == customer.Id, cancellationToken);
        if (existing is null)
        {
            return CustomerOperationResult.Fail("Customer not found.");
        }

        Normalize(customer);

        existing.Name = customer.Name;
        existing.BusinessName = customer.BusinessName;
        existing.Phone = customer.Phone;
        existing.Area = customer.Area;
        existing.Address = customer.Address;
        existing.Notes = customer.Notes;
        existing.CreditLimit = customer.CreditLimit;
        existing.DefaultCreditDays = customer.DefaultCreditDays;
        existing.IsActive = customer.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return CustomerOperationResult.Success(existing.Id);
        }
        catch (DbUpdateException)
        {
            return CustomerOperationResult.Fail("The customer could not be updated. Please review the details and try again.");
        }
    }

    public Task<decimal> GetCurrentBalanceAsync(int customerId, CancellationToken cancellationToken = default)
        => _ledgerService.GetCurrentBalanceAsync(customerId, cancellationToken);

    private static void Normalize(Customer customer)
    {
        customer.Name = customer.Name.Trim();
        customer.BusinessName = NullIfWhiteSpace(customer.BusinessName);
        customer.Phone = customer.Phone.Trim();
        customer.CnicOrIdentifier = NullIfWhiteSpace(customer.CnicOrIdentifier);
        customer.Area = NullIfWhiteSpace(customer.Area);
        customer.Address = NullIfWhiteSpace(customer.Address);
        customer.Notes = NullIfWhiteSpace(customer.Notes);
        customer.GuarantorName = NullIfWhiteSpace(customer.GuarantorName);
        customer.GuarantorPhone = NullIfWhiteSpace(customer.GuarantorPhone);
        customer.DeliveryNotes = NullIfWhiteSpace(customer.DeliveryNotes);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CustomerOperationResult(bool Succeeded, string? Error, int? CustomerId)
{
    public static CustomerOperationResult Success(int customerId) => new(true, null, customerId);
    public static CustomerOperationResult Fail(string error) => new(false, error, null);
}
