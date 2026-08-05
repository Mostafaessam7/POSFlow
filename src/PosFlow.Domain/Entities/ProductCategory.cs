namespace PosFlow.Domain.Entities;

public sealed class ProductCategory : BaseEntity
{
    public Guid TenantId { get; set; }

    public required string NameAr { get; set; }

    public string? NameEn { get; set; }
}
