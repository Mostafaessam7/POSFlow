namespace PosFlow.Application.Orders;

public sealed record VoidOrderRequest(
    string Reason
);
