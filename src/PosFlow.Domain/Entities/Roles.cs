namespace PosFlow.Domain.Entities;

/// <summary>
/// String forms of UserRole, matching UserRole.ToString() exactly
/// (the JWT role claim and [Authorize(Roles=...)] both work on these
/// strings, not the enum). Centralizing them here means a typo like
/// "Admn" fails to compile instead of silently locking everyone out
/// of an endpoint.
/// </summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Manager = nameof(UserRole.Manager);
    public const string Cashier = nameof(UserRole.Cashier);

    public const string AdminOrManager = Admin + "," + Manager;
}
