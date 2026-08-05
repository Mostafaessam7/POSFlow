namespace PosFlow.Application.Customers;

public sealed record UpdateCustomerRequest(
    string Name,
    string? Phone,
    string? Email,
    bool IsActive
);
