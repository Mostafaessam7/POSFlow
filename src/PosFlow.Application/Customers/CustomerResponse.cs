namespace PosFlow.Application.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string? Phone,
    string? Email,
    int LoyaltyPoints,
    bool IsActive
);
