namespace PosFlow.Domain.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public bool IsActive =>
        UsedAtUtc is null &&
        DateTime.UtcNow < ExpiresAtUtc;
}
