namespace PosFlow.Domain.Entities;

public sealed class Tenant : BaseEntity
{
    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Branch> Branches { get; set; } = [];
}