namespace PosFlow.Application.Auth;

public sealed record VerifyTwoFactorRequest(
    string ChallengeToken,
    string Code
);
