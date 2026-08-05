using PosFlow.Domain.Entities;

namespace PosFlow.Application.Orders;

public sealed record PaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    string? ReferenceNumber
);
