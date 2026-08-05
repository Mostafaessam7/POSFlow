namespace PosFlow.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginResponse?> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default);

    Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        ResetPasswordWithTokenRequest request,
        CancellationToken cancellationToken = default);
}