namespace PosFlow.Application.Orders;

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    string Status,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal ChangeDue,
    Guid? CustomerId,
    DateTime CreatedAtUtc,
    string? VoidReason,
    DateTime? VoidedAtUtc,
    IReadOnlyList<OrderLineResponse> Lines,
    IReadOnlyList<PaymentResponse> Payments
);
