using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Customers;

[Authorize(Policy = AppPermissions.ViewCustomers)]
public class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly LedgerService _ledgerService;

    public DetailsModel(ApplicationDbContext db, LedgerService ledgerService)
    {
        _db = db;
        _ledgerService = ledgerService;
    }

    public Customer Customer { get; private set; } = null!;
    public decimal OpeningBalance { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public decimal TotalSales { get; private set; }
    public decimal TotalPayments { get; private set; }
    public IReadOnlyList<CustomerLedgerEntry> RecentEntries { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (customer is null)
        {
            return NotFound();
        }

        var summary = await _ledgerService.GetAccountSummaryAsync(id);
        if (summary is null)
        {
            return NotFound();
        }

        Customer = customer;
        OpeningBalance = summary.OpeningBalance;
        CurrentBalance = summary.CurrentBalance;
        TotalSales = summary.TotalSales;
        TotalPayments = summary.TotalPayments;
        RecentEntries = summary.RecentEntries;

        return Page();
    }
}
