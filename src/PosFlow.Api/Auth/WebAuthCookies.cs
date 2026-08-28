namespace PosFlow.Api.Auth;

/// <summary>
/// Carries the refresh token in an <c>HttpOnly</c> cookie instead of the JSON body, so it is
/// unreachable from JavaScript.
///
/// Why this matters here specifically: the Angular client previously kept both the access token and
/// the <em>refresh</em> token in <c>localStorage</c>. The access token is short-lived, but a stolen
/// refresh token is a renewable session — any XSS anywhere in the app, or one compromised npm
/// dependency, could read it and mint access tokens indefinitely. An <c>HttpOnly</c> cookie is not
/// readable even by a payload that has already hooked <c>fetch</c>/<c>XHR</c>.
///
/// Moving a credential into a cookie means the browser now attaches it automatically, which is
/// exactly what CSRF exploits — so the double-submit check below ships in the same change, not
/// after it. A cookie-carried session without CSRF protection trades one vulnerability for another.
///
/// Transport is opt-in via <c>X-Auth-Transport: cookie</c>, matching the pattern already proven in
/// RealEstateCRM. Non-browser callers (the terminal agent, integration tests, curl) keep the
/// body-based flow untouched.
/// </summary>
public static class WebAuthCookies
{
    public const string RefreshTokenCookieName = "posflow_rt";

    /// <summary>
    /// Deliberately NOT HttpOnly: the SPA has to read this one and echo it back in a header. That
    /// is the whole double-submit mechanism — an attacker's site can cause the browser to *send*
    /// cookies cross-origin, but the same-origin policy stops it *reading* them, so it cannot
    /// produce a matching header.
    /// </summary>
    public const string CsrfCookieName = "XSRF-TOKEN";

    public const string CsrfHeaderName = "X-XSRF-TOKEN";

    public const string TransportHeaderName = "X-Auth-Transport";

    /// <summary>
    /// True when the caller asked for cookie transport, or already holds a refresh cookie. The
    /// second condition matters for refresh/logout, where the client may not resend the opt-in
    /// header but the cookie proves the session was established that way.
    /// </summary>
    public static bool UsesCookieTransport(HttpRequest request) =>
        string.Equals(request.Headers[TransportHeaderName], "cookie", StringComparison.OrdinalIgnoreCase)
        || request.Cookies.ContainsKey(RefreshTokenCookieName);

    /// <summary>
    /// Double-submit validation. Only meaningful once the credential travels in a cookie; for
    /// body-based callers there is nothing to forge on their behalf.
    /// </summary>
    public static bool HasValidCsrfToken(HttpRequest request)
    {
        var cookieValue = request.Cookies[CsrfCookieName];
        var headerValue = request.Headers[CsrfHeaderName].ToString();

        return !string.IsNullOrEmpty(cookieValue)
            && !string.IsNullOrEmpty(headerValue)
            // Ordinal, not culture-aware: these are opaque tokens, and culture-sensitive comparison
            // can treat distinct byte sequences as equal.
            && string.Equals(cookieValue, headerValue, StringComparison.Ordinal);
    }

    public static void Issue(HttpResponse response, string refreshToken, DateTime expiresAtUtc, bool isDevelopment)
    {
        // SameSite=None is required when the SPA and API sit on different origins, and browsers
        // reject SameSite=None without Secure. Local development has no TLS, so it uses Lax
        // instead — differing ports do not change the SameSite "site" definition, so Lax still
        // works for a localhost dev loop.
        var sameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
        var secure = !isDevelopment;

        response.Cookies.Append(RefreshTokenCookieName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
            // Scoped to the auth endpoints: the refresh token has no business being attached to
            // every product/order request, and a narrower path is a smaller surface if any single
            // endpoint ever leaks headers.
            Path = "/api/auth",
        });

        // A fresh CSRF token per issue, so it rotates with the session rather than living as long
        // as the browser profile.
        response.Cookies.Append(CsrfCookieName, Guid.NewGuid().ToString("N"), new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = sameSite,
            Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
            Path = "/",
        });
    }

    public static void Clear(HttpResponse response, bool isDevelopment)
    {
        var sameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
        var secure = !isDevelopment;

        // Attributes must match those used when the cookie was set, or the browser treats this as
        // a different cookie and the original survives the logout.
        response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = secure,
            SameSite = sameSite,
            Path = "/api/auth",
        });

        response.Cookies.Delete(CsrfCookieName, new CookieOptions
        {
            HttpOnly = false,
            Secure = secure,
            SameSite = sameSite,
            Path = "/",
        });
    }
}
