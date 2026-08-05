namespace PosFlow.Application.Users;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    string? Email,
    string Role,
    Guid? BranchId,
    bool IsActive
);
