namespace PosFlow.Domain.Entities;

public enum UserRole
{
    Admin = 1,
    Manager = 2,
    Cashier = 3
}

public sealed class AppUser : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public required string Username { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string DisplayName { get; set; }

    public string? Email { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Cashier;

    public bool IsActive { get; set; } = true;

    /// <summary>TOTP (RFC 6238) two-factor auth - off by default, opt-in per user. Strongly recommended for Admin accounts.</summary>
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Base32-encoded TOTP secret. Null until the user completes 2FA setup. Never returned by any API response.</summary>
    public string? TwoFactorSecret { get; set; }

    /// <summary>Consecutive failed password checks since the last successful login. Reset to 0 on success.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Set once <see cref="FailedLoginAttempts"/> crosses the lockout threshold. Null when the account isn't locked. Login is rejected while this is in the future, regardless of password correctness.</summary>
    public DateTime? LockoutEndUtc { get; set; }
}