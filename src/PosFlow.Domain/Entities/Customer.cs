namespace PosFlow.Domain.Entities;

public sealed class Customer : BaseEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public int LoyaltyPoints { get; set; }

    public bool IsActive { get; set; } = true;
}
