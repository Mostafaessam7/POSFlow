namespace PosFlow.Application.Orders;

public sealed record PaymentResponse(
    Guid Id,
    string Method,
    decimal Amount,
    string? ReferenceNumber
);
