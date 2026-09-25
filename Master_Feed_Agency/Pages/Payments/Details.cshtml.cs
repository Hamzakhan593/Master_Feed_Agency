using System.Security.Claims;
using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Master_Feed_Agency.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Payments;

[Authorize(Policy = AppPermissions.ReceivePayments)]
public sealed class DetailsModel : PageModel
{
    private readonly ApplicationDbContext _db; private readonly PaymentService _service;
    public DetailsModel(ApplicationDbContext db, PaymentService service){_db=db;_service=service;}
    public Payment Payment { get; private set; } = null!;
    public string ReceiverName { get; private set; } = "";
    public decimal RemainingBalance { get; private set; }
    [BindProperty] public string ReversalReason { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(long id,CancellationToken ct)=>await Load(id,ct)?Page():NotFound();

    public async Task<IActionResult> OnPostReverseAsync(long id,CancellationToken ct)
    {
        if (!User.HasPermission(AppPermissions.ReversePayments)) return Forbid();
        var userId=User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result=await _service.ReverseAsync(id,ReversalReason,userId,ct);
        TempData[result.Succeeded?"SuccessMessage":"ErrorMessage"] = result.Succeeded?"Payment reversed successfully.":result.Error;
        return RedirectToPage(new{id});
    }

    private async Task<bool> Load(long id,CancellationToken ct)
    {
        var payment=await _db.Payments.AsNoTracking().Include(x=>x.Customer).Include(x=>x.Allocations).ThenInclude(x=>x.Sale).FirstOrDefaultAsync(x=>x.Id==id,ct)!;
        if(payment is null)return false;
        Payment=payment;
        ReceiverName=await _db.Users.Where(x=>x.Id==Payment.ReceivedByUserId).Select(x=>x.FullName).FirstOrDefaultAsync(ct) ?? Payment.ReceivedByUserId;
        RemainingBalance=await _db.CustomerLedgerEntries.Where(x=>x.CustomerId==Payment.CustomerId).SumAsync(x=>(decimal?)(x.Debit-x.Credit),ct) ?? 0m;
        return true;
    }
}
