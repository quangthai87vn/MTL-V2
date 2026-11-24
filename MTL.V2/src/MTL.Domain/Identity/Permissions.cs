namespace MTL.Domain.Identity;

public static class Permissions
{
    public const string DashboardRead = "dashboard.read";
    public const string EmployeesRead = "employees.read";
    public const string EmployeesManage = "employees.manage";
    public const string RolesManage = "roles.manage";
    public const string ProductsRead = "products.read";
    public const string ProductsManage = "products.manage";
    public const string WooCommerceManage = "woocommerce.manage";
    public const string SalesRead = "sales.read";
    public const string SalesManage = "sales.manage";
    public const string PurchasesRead = "purchases.read";
    public const string PurchasesManage = "purchases.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersManage = "customers.manage";
    public const string InventoryRead = "inventory.read";
    public const string InventoryManage = "inventory.manage";
    public const string ReportsRead = "reports.read";
    public const string SettingsManage = "settings.manage";
    public const string AuditRead = "audit.read";

    public static readonly IReadOnlyList<string> All =
    [
        DashboardRead,
        EmployeesRead,
        EmployeesManage,
        RolesManage,
        ProductsRead,
        ProductsManage,
        WooCommerceManage,
        SalesRead,
        SalesManage,
        PurchasesRead,
        PurchasesManage,
        CustomersRead,
        CustomersManage,
        InventoryRead,
        InventoryManage,
        ReportsRead,
        SettingsManage,
        AuditRead
    ];
}

public static class SystemRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Sales = "Sales";
    public const string Warehouse = "Warehouse";
}

