namespace PosFlow.Domain.Entities;

public sealed class OrderLine : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }

    public Order Order { get; set; } = null!;
}