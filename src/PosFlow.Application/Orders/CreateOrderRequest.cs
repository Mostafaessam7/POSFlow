namespace PosFlow.Application.Orders;

public sealed record CreateOrderRequest(
    IReadOnlyList<OrderLineRequest> Lines,
    IReadOnlyList<PaymentRequest> Payments
);
