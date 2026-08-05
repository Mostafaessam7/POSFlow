namespace PosFlow.Application.Users;

public sealed record ResetPasswordRequest(
    string NewPassword
);
