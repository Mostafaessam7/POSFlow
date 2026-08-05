namespace PosFlow.Application.Auth;

public sealed record EnableTwoFactorRequest(string Code);

public sealed record DisableTwoFactorRequest(string Code);
