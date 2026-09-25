using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Master_Feed_Agency.Pages.Payments;
[Authorize(Policy=AppPermissions.ReceivePayments)]
public sealed class ReceiptModel:PageModel
{
 private readonly ApplicationDbContext _db; public ReceiptModel(ApplicationDbContext db)=>_db=db;
 public Payment Payment {get;private set;}=null!;
 public async Task<IActionResult> OnGetAsync(long id,CancellationToken ct){var payment=await _db.Payments.AsNoTracking().Include(x=>x.Customer).FirstOrDefaultAsync(x=>x.Id==id,ct)!;if(payment is null)return NotFound();Payment=payment;return Page();}
}
