namespace PosFlow.Domain.Entities;

public enum OrderStatus
{
    Draft = 1,
    Completed = 2,
    Cancelled = 3
}

public sealed class Order : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ShiftId { get; set; }

    public string OrderNumber { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    public string? VoidReason { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public ICollection<OrderLine> Lines { get; set; } = [];

    public ICollection<Payment> Payments { get; set; } = [];
}