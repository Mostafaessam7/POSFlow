namespace PosFlow.Domain.Entities;

public sealed class Branch : BaseEntity
{
    public Guid TenantId { get; set; }

    public required string Name { get; set; }

    public required string Code { get; set; }

    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
}