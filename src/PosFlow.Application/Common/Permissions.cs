namespace PosFlow.Application.Common;

/// <summary>
/// Fine-grained permission catalog. Controllers authorize against
/// these (via ASP.NET Core policies named identically - see
/// Program.cs) instead of raw [Authorize(Roles = "Admin,Manager")]
/// strings, so:
///  - the intent of each endpoint is self-documenting here in one
///    place, instead of scattered role-string literals;
///  - a future per-user permission override (instead of the current
///    fixed role -> permission-set mapping in RolePermissions) is a
///    change to the permission-resolution logic only, not to every
///    controller.
/// </summary>
public static class Permissions
{
    public const string ProductsRead = "products.read";
    public const string ProductsWrite = "products.write";

    public const string CategoriesWrite = "categories.write";

    public const string OrdersCheckout = "orders.checkout";
    public const string OrdersVoid = "orders.void";

    public const string ShiftsManageOwn = "shifts.manage-own";
    public const string ShiftsViewBranchHistory = "shifts.view-branch-history";

    public const string UsersManage = "users.manage";
    public const string BranchesManage = "branches.manage";
    public const string CustomersManage = "customers.manage";
    public const string TenantSettingsManage = "tenant-settings.manage";

    public const string ReportsView = "reports.view";
    public const string AuditLogView = "audit-log.view";

    /// <summary>Every permission that exists - Program.cs registers one AspNetCore authorization policy per entry.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        ProductsRead, ProductsWrite, CategoriesWrite,
        OrdersCheckout, OrdersVoid,
        ShiftsManageOwn, ShiftsViewBranchHistory,
        UsersManage, BranchesManage, CustomersManage,
        TenantSettingsManage, ReportsView, AuditLogView
    ];
}
