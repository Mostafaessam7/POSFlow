namespace PosFlow.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<LoginResponse?> VerifyTwoFactorAsync(
        VerifyTwoFactorRequest request,
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

    /// <summary>Generates a new TOTP secret and persists it on the user (TwoFactorEnabled stays false until EnableTwoFactorAsync confirms a code against it).</summary>
    Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Verifies code against the secret from BeginTwoFactorSetupAsync and, if valid, turns 2FA on. Returns false (does not throw) on an invalid code.</summary>
    Task<bool> EnableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    Task<bool> DisableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);
}
