namespace PosFlow.Application.Users;

public sealed record UpdateUserRequest(
    string DisplayName,
    string? Email,
    string Role,
    Guid? BranchId,
    bool IsActive
);
