namespace PosFlow.Application.Auth;

public sealed record LoginRequest(
    string Username,
    string Password
);