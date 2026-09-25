using Master_Feed_Agency.Data;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Customers;

[Authorize(Policy = AppPermissions.ViewCustomers)]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly LedgerService _ledgerService;

    public IndexModel(ApplicationDbContext db, LedgerService ledgerService)
    {
        _db = db;
        _ledgerService = ledgerService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "active";

    public List<CustomerRow> Customers { get; private set; } = [];

    public sealed record CustomerRow(
        int Id,
        string Name,
        string? BusinessName,
        string Phone,
        string? Area,
        decimal CreditLimit,
        int DefaultCreditDays,
        decimal CurrentBalance,
        bool IsActive);

    public async Task OnGetAsync()
    {
        Status = string.IsNullOrWhiteSpace(Status) ? "active" : Status;
        var query = _db.Customers.AsNoTracking();
        var search = Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Name.Contains(search) ||
                (x.BusinessName != null && x.BusinessName.Contains(search)) ||
                x.Phone.Contains(search) ||
                (x.CnicOrIdentifier != null && x.CnicOrIdentifier.Contains(search)) ||
                (x.Area != null && x.Area.Contains(search)));
        }

        if (string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(Status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        var customers = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.BusinessName)
            .ToListAsync();

        var balances = await _ledgerService.GetBalancesAsync(customers.Select(x => x.Id));

        Customers = customers.Select(x => new CustomerRow(
            x.Id,
            x.Name,
            x.BusinessName,
            x.Phone,
            x.Area,
            x.CreditLimit,
            x.DefaultCreditDays,
            balances.GetValueOrDefault(x.Id),
            x.IsActive)).ToList();
    }
}
