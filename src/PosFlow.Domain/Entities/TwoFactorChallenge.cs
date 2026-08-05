namespace PosFlow.Domain.Entities;

/// <summary>
/// Short-lived record bridging LoginAsync (password verified, but 2FA
/// still owed) and VerifyTwoFactorAsync (TOTP code checked, real
/// tokens issued). The opaque ChallengeToken handed to the client
/// carries no claims and cannot be used to call any API endpoint on
/// its own - only to complete this specific login.
/// </summary>
public sealed class TwoFactorChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string ChallengeTokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public bool IsActive =>
        UsedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
