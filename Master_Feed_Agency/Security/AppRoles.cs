namespace Master_Feed_Agency.Security;

public static class AppRoles
{
    public const string Owner = "Owner";
    public const string Manager = "Manager";
    public const string Accountant = "Accountant";
    public const string Salesman = "Salesman";

    public static readonly string[] All = [Owner, Manager, Accountant, Salesman];
}
