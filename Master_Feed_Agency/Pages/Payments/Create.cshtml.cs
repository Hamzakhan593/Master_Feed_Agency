using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Master_Feed_Agency.Pages.Payments;

[Authorize(Policy = AppPermissions.ReceivePayments)]
public sealed class CreateModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly PaymentService _payments;
    public CreateModel(ApplicationDbContext db, PaymentService payments){_db=db;_payments=payments;}

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<SelectListItem> Customers { get; private set; } = [];
    public Dictionary<int, decimal> Balances { get; private set; } = [];

    public sealed class InputModel
    {
        [Range(1,int.MaxValue,ErrorMessage="Customer search karke select karein.")] public int CustomerId { get; set; }
        [Range(typeof(decimal),"0.01","999999999999",ErrorMessage="Mili hui payment ki sahi raqam likhein.")] public decimal Amount { get; set; }
        public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
        [StringLength(500)] public string? Notes { get; set; }
        [Required] public string ClientRequestId { get; set; } = Guid.NewGuid().ToString("N");
    }

    public async Task OnGetAsync(int? customerId, CancellationToken ct)
    {
        Input.CustomerId = customerId ?? 0;
        await LoadAsync(ct);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await LoadAsync(ct);
        if (!ModelState.IsValid) return Page();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _payments.ReceiveAsync(new PaymentPostRequest(Input.CustomerId,Input.Amount,Input.Method,Input.Notes,Input.ClientRequestId),userId,ct);
        if (!result.Succeeded){ModelState.AddModelError(string.Empty,result.Error!);return Page();}
        TempData["SuccessMessage"] = result.DuplicateSubmission ? $"Payment pehle se saved hai: {result.ReceiptNo}." : $"Payment save ho gayi. Receipt {result.ReceiptNo}.";
        return RedirectToPage("Details",new{id=result.PaymentId});
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        Customers = await _db.Customers.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.Name)
            .Select(x=>new SelectListItem($"{x.Name} - {x.Phone} - {x.BusinessName}",x.Id.ToString())).ToListAsync(ct);
        Balances = await _db.CustomerLedgerEntries.AsNoTracking().GroupBy(x => x.CustomerId)
            .Select(g => new { Id = g.Key, Balance = g.Sum(x => x.Debit - x.Credit) })
            .ToDictionaryAsync(x => x.Id, x => x.Balance, ct);
    }
}
