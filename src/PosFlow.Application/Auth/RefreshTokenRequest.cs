namespace PosFlow.Application.Auth;

/// <summary>
/// The refresh credential as it arrives from a client.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RefreshToken"/> is nullable because it is genuinely optional: browser clients carry
/// the token in an HttpOnly cookie and post an empty body on purpose, so that the credential never
/// passes through JavaScript. Non-browser clients still send it here.
/// </para>
/// <para>
/// The nullability is load-bearing, not cosmetic. With a non-nullable <c>string</c>, ASP.NET Core's
/// implicit-required rule for non-nullable reference types rejected the empty body with
/// "The RefreshToken field is required." before the action ever ran — so the controller's
/// cookie fallback was unreachable and every page reload logged the user out. Removing the
/// FluentValidation validator was not enough to prevent that, because the 400 came from model
/// binding rather than from a validator.
/// </para>
/// <para>
/// Which transport actually carried the credential stays an API-layer decision; the presence check
/// lives in the controller, where both the cookie and the body are visible.
/// </para>
/// </remarks>
public sealed record RefreshTokenRequest(
    string? RefreshToken
);
