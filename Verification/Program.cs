using Master_Feed_Agency.Data;
using Master_Feed_Agency.Models;
using Master_Feed_Agency.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

// Never use an agency database. This executable creates its own isolated verification database.
var connection = "Server=(localdb)\\MSSQLLocalDB;Database=MasterFeed_Clean_Verification_20260924;Trusted_Connection=True;TrustServerCertificate=True";
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection).Options);
if (args.Contains("--cleanup")) { await db.Database.EnsureDeletedAsync(); Console.WriteLine("Verification database removed."); return; }
if (await db.Database.CanConnectAsync()) throw new Exception("Verification database already exists; use --cleanup before running again.");
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
Check(!db.Database.HasPendingModelChanges(), "Current model matches migration snapshot");
// Simulate a populated installation upgraded from the supplied ZIP.
await db.GetService<IMigrator>().MigrateAsync("20260920041115_mulfiles");
await db.Database.ExecuteSqlRawAsync("INSERT INTO Owners (Name, IsActive, CreatedAt, CreatedByUserId) VALUES ('Historical owner', 1, GETUTCDATE(), 'verification')");
await db.Database.MigrateAsync();
Check(await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Owners WHERE Name='Historical owner'").SingleAsync() == 1, "Upgrade preserves retired historical data");
Check(!db.Model.GetEntityTypes().Any(x => x.ClrType.Name is "Owner" or "Expense" or "CashTransaction" or "AuditLog" or "NotificationMessage"), "Retired modules are absent from the application model");
var stock = new StockService(db); var ledger = new LedgerService(db); var customers = new CustomerService(db, ledger);
var sales = new SaleService(db); var payments = new PaymentService(db);
var product = new Product {Code="FEED-01",Name="Broiler Feed",Unit="Bag",DefaultSalePrice=6500,LowStockThreshold=10,IsActive=true};
Check((await stock.CreateProductAsync(product,100,"verification")).Succeeded,"Product opening stock");
var customer = new Customer {Name="Ahmed Poultry Farm",Phone="03001234567",Area="Lahore",CreditLimit=1000000,DefaultCreditDays=7};
Check((await customers.CreateCustomerAsync(customer,500,"verification")).Succeeded,"Customer opening balance");
customer.CnicOrIdentifier = "historical-id";
customer.GuarantorName = "Historical guarantor";
customer.DeliveryNotes = "Historical directions";
await db.SaveChangesAsync();
var edited = new Customer { Id=customer.Id, Name=customer.Name, Phone=customer.Phone, CreditLimit=customer.CreditLimit, DefaultCreditDays=7, IsActive=true };
Check((await customers.UpdateCustomerAsync(edited,"verification")).Succeeded && customer.CnicOrIdentifier=="historical-id" && customer.GuarantorName=="Historical guarantor" && customer.DeliveryNotes=="Historical directions", "Simple customer edit preserves historical metadata");
SalePostRequest Request(string key, SaleType type, decimal qty, decimal paid=0) => new(key,customer.Id,type,0,paid,PaymentMethod.Cash,BusinessTime.Today.AddDays(7),null,[new(product.Id,qty,6500)]);
var cash = await sales.PostSaleAsync(Request("test-cash",SaleType.Cash,2),"verification",false);
Check(cash.Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==500 && await stock.GetCurrentStockAsync(product.Id)==98,"Cash sale preserves balance and deducts stock");
var credit = await sales.PostSaleAsync(Request("test-credit",SaleType.Credit,3),"verification",false);
Check(credit.Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==20000,"Credit sale increases balance");
var duplicate = await sales.PostSaleAsync(Request("test-credit",SaleType.Credit,3),"verification",false);
Check(duplicate.DuplicateSubmission && await stock.GetCurrentStockAsync(product.Id)==95,"Repeated sale does not duplicate stock or balance");
var partial = await sales.PostSaleAsync(Request("test-partial",SaleType.PartPaymentCredit,2,3000),"verification",false);
Check(partial.Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==30000,"Part payment leaves correct udhaar");
var rejected = await sales.PostSaleAsync(Request("test-no-stock",SaleType.Credit,1000),"verification",false);
Check(!rejected.Succeeded && await stock.GetCurrentStockAsync(product.Id)==93,"Insufficient stock fails without changes");
var payment = await payments.ReceiveAsync(new(customer.Id,5000,PaymentMethod.Cash,null,"test-payment"),"verification");
Check(payment.Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==25000,"Payment reduces balance");
var dash = await new DashboardService(db,new CreditService(db)).GetSnapshotAsync();
Check(dash.TodaySaleReceipts==16000 && dash.TodayRecovery==5000 && dash.TodaySaleReceipts+dash.TodayRecovery==21000,"Dashboard separates sale receipts from later payments without double counting");
Check((await payments.ReceiveAsync(new(customer.Id,5000,PaymentMethod.Cash,null,"test-payment"),"verification")).DuplicateSubmission && await ledger.GetCurrentBalanceAsync(customer.Id)==25000,"Repeated payment is idempotent");
Check(!(await payments.ReceiveAsync(new(customer.Id,999999,PaymentMethod.Cash,null,"test-too-large"),"verification")).Succeeded,"Overpayment is rejected");
Check(!(await payments.ReceiveAsync(new(customer.Id,.001m,PaymentMethod.Cash,null,"test-tiny"),"verification")).Succeeded,"Zero after rounding is rejected");
Check(!(await payments.ReceiveAsync(new(customer.Id,1,(PaymentMethod)999,null,"test-method"),"verification")).Succeeded,"Invalid payment method is rejected");
Check(!(await sales.CancelSaleAsync(credit.SaleId!.Value,"Test cancellation","verification")).Succeeded,"Sale with received payment cannot be cancelled before reversing payment");
Check((await payments.ReverseAsync(payment.PaymentId!.Value,"Test correction","verification")).Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==30000,"Payment reversal restores balance");
Check((await sales.CancelSaleAsync(credit.SaleId.Value,"Test cancellation","verification")).Succeeded && await ledger.GetCurrentBalanceAsync(customer.Id)==10500 && await stock.GetCurrentStockAsync(product.Id)==96,"Credit cancellation restores stock and balance");
Check((await sales.CancelSaleAsync(credit.SaleId.Value,"Test repeat","verification")).AlreadyCancelled && await stock.GetCurrentStockAsync(product.Id)==96,"Repeated cancellation is idempotent");
var dashAfter = await new DashboardService(db,new CreditService(db)).GetSnapshotAsync();
Check(dashAfter.TodaySaleReceipts==16000 && dashAfter.TodayRecovery==0,"Dashboard excludes reversed payments");
var summary=await ledger.GetAccountSummaryAsync(customer.Id);
Check(summary!.TotalSales==26000 && summary.TotalPayments==16000 && summary.CurrentBalance==10500,"Customer totals reconcile after reversals");
var statement=await ledger.GetStatementAsync(customer.Id,BusinessTime.Today,BusinessTime.Today);
Check(statement!.ClosingBalance==10500 && statement.Rows.Any(x=>x.PaymentId==payment.PaymentId),"Khata date filter and payment receipt links");
var reports=new ReportService(db,new CreditService(db));
Check((await reports.GetSalesAsync(BusinessTime.Today,BusinessTime.Today)).NetTotal==26000,"Sales report excludes cancelled sale");
Check((await reports.GetStockAsync(BusinessTime.Today,BusinessTime.Today)).Rows.Single().ClosingStock==96,"Stock report reconciles");
var overdueSale = await db.Sales.SingleAsync(x=>x.Id==partial.SaleId);
overdueSale.DueDate=BusinessTime.Today.AddDays(-1);await db.SaveChangesAsync();
var dueDashboard=await new DashboardService(db,new CreditService(db)).GetSnapshotAsync();
Check(dueDashboard.DueCustomers.Any(x=>x.CustomerId==customer.Id && x.Amount>0),"Dashboard follow-up includes overdue customers");
db.ChangeTracker.Clear();
db.Payments.Remove(await db.Payments.FirstAsync());
bool deletionBlocked = false;
try { await db.SaveChangesAsync(); }
catch (InvalidOperationException) { deletionBlocked = true; db.ChangeTracker.Clear(); }
Check(deletionBlocked, "Posted payment deletion is blocked");
Console.WriteLine("ALL CHECKS PASSED. Verification data retained for browser checks; use --cleanup afterwards.");
