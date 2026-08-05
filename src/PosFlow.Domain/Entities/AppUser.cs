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
}