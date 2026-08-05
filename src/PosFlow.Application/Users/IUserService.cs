namespace PosFlow.Application.Users;

public interface IUserService
{
    Task<IReadOnlyList<UserResponse>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<UserResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<UserResponse> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserResponse> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        Guid id,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
