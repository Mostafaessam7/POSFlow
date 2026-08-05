namespace PosFlow.Application.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    Guid UserId,
    Guid TenantId,
    Guid? BranchId,
    string DisplayName,
    string Role
);