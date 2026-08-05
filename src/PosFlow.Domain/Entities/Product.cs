namespace PosFlow.Domain.Entities;

public sealed class Product : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid? CategoryId { get; set; }

    public required string NameAr { get; set; }

    public string? NameEn { get; set; }

    public string? Barcode { get; set; }

    public decimal Price { get; set; }

    public bool TrackStock { get; set; }

    public decimal StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;
}
