using System.ComponentModel.DataAnnotations;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Master_Feed_Agency.Pages.Customers;

[Authorize(Policy = AppPermissions.ViewLedger)]
public class KhataModel : PageModel
{
    private readonly LedgerService _ledgerService;

    public KhataModel(LedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public CustomerStatement Statement { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        DateTime? queryFrom = From;
        DateTime? queryTo = To;

        if (From.HasValue && To.HasValue && From.Value.Date > To.Value.Date)
        {
            ModelState.AddModelError(string.Empty, "From date cannot be after To date. Showing the complete khata instead.");
            queryFrom = null;
            queryTo = null;
        }

        var statement = await _ledgerService.GetStatementAsync(id, queryFrom, queryTo);
        if (statement is null)
        {
            return NotFound();
        }

        Statement = statement;
        return Page();
    }
}
