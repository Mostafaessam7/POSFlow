using PosFlow.Domain.Entities;

namespace PosFlow.Application.Common;

/// <summary>
/// Default permission set per fixed role. This is the ONE place that
/// decides "what can a Cashier/Manager/Admin do" - everything else
/// (the authorization handler, controllers) just asks "does this
/// user's role grant permission X".
/// </summary>
public static class RolePermissions
{
    private static readonly IReadOnlyDictionary<UserRole, HashSet<string>> Map =
        new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.Cashier] =
            [
                Permissions.ProductsRead,
                Permissions.OrdersCheckout,
                Permissions.ShiftsManageOwn
            ],

            [UserRole.Manager] =
            [
                Permissions.ProductsRead,
                Permissions.ProductsWrite,
                Permissions.CategoriesWrite,
                Permissions.OrdersCheckout,
                Permissions.OrdersVoid,
                Permissions.ShiftsManageOwn,
                Permissions.ShiftsViewBranchHistory,
                Permissions.CustomersManage,
                Permissions.ReportsView
            ],

            [UserRole.Admin] = [.. Permissions.All]
        };

    public static bool RoleHasPermission(string role, string permission)
    {
        return Enum.TryParse<UserRole>(role, out var parsedRole) &&
            Map.TryGetValue(parsedRole, out var permissions) &&
            permissions.Contains(permission);
    }
}
