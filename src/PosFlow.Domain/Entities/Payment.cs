namespace PosFlow.Domain.Entities;

public enum PaymentMethod
{
    Cash = 1,
    Card = 2
}

public sealed class Payment : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid OrderId { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }

    public Order Order { get; set; } = null!;
}