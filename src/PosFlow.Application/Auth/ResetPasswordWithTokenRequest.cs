namespace PosFlow.Application.Auth;

public sealed record ResetPasswordWithTokenRequest(
    string Token,
    string NewPassword
);
