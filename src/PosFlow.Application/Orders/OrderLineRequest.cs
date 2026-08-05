namespace PosFlow.Application.Orders;

public sealed record OrderLineRequest(
    Guid ProductId,
    decimal Quantity,
    decimal DiscountAmount = 0
);
