namespace PosFlow.Application.Users;

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    string? Email,
    string Password,
    string Role,
    Guid? BranchId
);
