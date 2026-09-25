namespace Master_Feed_Agency.Security;

public sealed record PermissionDefinition(string Value, string Label, string Group);

public static class AppPermissions
{
    public const string ClaimType = "Permission";

    public const string ViewDashboard = "Dashboard.View";
    public const string ManageUsers = "Users.Manage";
    public const string ViewProducts = "Products.View";
    public const string ManageProducts = "Products.Manage";
    public const string AdjustStock = "Stock.Adjust";
    public const string OverrideNegativeStock = "Stock.OverrideNegative";
    public const string ViewCustomers = "Customers.View";
    public const string ManageCustomers = "Customers.Manage";
    public const string CreateSales = "Sales.Create";
    public const string CancelSales = "Sales.Cancel";
    public const string ViewLedger = "Ledger.View";
    public const string ReceivePayments = "Payments.Receive";
    public const string ReversePayments = "Payments.Reverse";
    public const string ViewReports = "Reports.View";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(ViewDashboard, "Business Summary dekhein", "General"),
        new(ManageUsers, "Staff Accounts Aur Ijazat Manage Karein", "Administration"),
        new(ViewProducts, "Products Aur Stock Dekhein", "Products / Stock"),
        new(ManageProducts, "Products Add Ya Edit Karein", "Products / Stock"),
        new(AdjustStock, "Stock add ya correct karein", "Products / Stock"),
        new(OverrideNegativeStock, "Stock se zyada sale ki ijazat", "Products / Stock"),
        new(ViewCustomers, "Customers Dekhein", "Customers"),
        new(ManageCustomers, "Customers Add Ya Edit Karein", "Customers"),
        new(CreateSales, "Nayi sale save karein", "Sales"),
        new(CancelSales, "Bill Cancel Karein", "Sales"),
        new(ViewLedger, "Customer khata dekhein", "Customer Payments"),
        new(ReceivePayments, "Payment Wasool Karein", "Customer Payments"),
        new(ReversePayments, "Ghalat Payment Cancel Karein", "Customer Payments"),
        new(ViewReports, "Business Reports Dekhein", "Reports"),
    ];

    public static IReadOnlyCollection<string> DefaultsForRole(string role) => role switch
    {
        AppRoles.Owner => All.Select(x => x.Value).ToArray(),

        AppRoles.Manager =>
        [
            ViewDashboard, ViewProducts, ManageProducts, AdjustStock,
            ViewCustomers, ManageCustomers, CreateSales, CancelSales,
            ViewLedger, ReceivePayments,
            ViewReports, ],

        AppRoles.Accountant =>
        [
            ViewDashboard, ViewProducts, ViewCustomers, ViewLedger, ReceivePayments, ReversePayments, ViewReports, ],

        AppRoles.Salesman =>
        [
            ViewDashboard, ViewProducts, ViewCustomers, CreateSales,
            ViewLedger, ReceivePayments, ],

        _ => Array.Empty<string>()
    };
}
