namespace PosFlow.Application.Auth;

/// <summary>
/// Either a completed login (TwoFactorRequired: false, tokens
/// populated) or a 2FA challenge (TwoFactorRequired: true,
/// ChallengeToken populated, no tokens yet - call
/// POST /api/auth/login/verify-2fa with the ChallengeToken and a TOTP
/// code to get real tokens).
/// </summary>
public sealed record LoginResponse(
    bool TwoFactorRequired,
    string? ChallengeToken,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    string? RefreshToken,
    Guid? UserId,
    Guid? TenantId,
    Guid? BranchId,
    string? DisplayName,
    string? Role
);
