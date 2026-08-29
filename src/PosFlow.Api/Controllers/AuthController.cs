using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PosFlow.Api.Auth;
using PosFlow.Application.Auth;

namespace PosFlow.Api.Controllers;

// The strict 5/min "auth" limiter is applied per-action below, only to
// endpoints an anonymous attacker could hammer to brute-force a
// credential: login, 2FA verification, and password reset. Refresh and
// logout moved to the looser "auth-session" policy - neither takes a
// guessable secret, and refresh runs on every page load, so the strict
// limit was signing users out after a few reloads (see Program.cs). The
// authenticated 2FA management endpoints (setup/enable/disable) are
// already gated by [Authorize] and covered by the global rate limiter
// instead - a legitimate logged-in user completing 2FA setup can
// easily make more than 5 requests/minute (setup, enable, a few
// retries) without that being remotely brute-force-shaped.
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IWebHostEnvironment environment)
    : ControllerBase
{
    private readonly IAuthService _authService = authService;
    private readonly IWebHostEnvironment _environment = environment;

    /// <summary>
    /// For a cookie-transport caller, moves the refresh token out of the JSON body and into an
    /// HttpOnly cookie. Returning it in both places would defeat the entire point — the token would
    /// still be readable by any script on the page.
    /// </summary>
    private ActionResult<LoginResponse> RespondWithSession(LoginResponse result)
    {
        if (!WebAuthCookies.UsesCookieTransport(Request) || string.IsNullOrEmpty(result.RefreshToken))
        {
            return Ok(result);
        }

        WebAuthCookies.Issue(
            Response,
            result.RefreshToken,
            // Refresh-token lifetime is owned by the auth service; the cookie is set to expire with
            // the access token's day boundary at the latest so a stale cookie is not left behind.
            result.ExpiresAtUtc?.AddDays(30) ?? DateTime.UtcNow.AddDays(30),
            _environment.IsDevelopment());

        return Ok(result with { RefreshToken = null });
    }

    /// <summary>
    /// Resolves which refresh token to act on. A cookie-carried token is only honoured when the
    /// double-submit CSRF check passes — the browser attaches the cookie automatically, so without
    /// this a third-party page could trigger a refresh or logout on the user's behalf.
    /// </summary>
    /// <remarks>
    /// The "a refresh token must be present" rule lives here rather than in a FluentValidation
    /// validator on <see cref="RefreshTokenRequest"/>. Validation runs before the action, and a
    /// validator in the Application layer cannot see cookies — it would have to reference
    /// ASP.NET Core, which is exactly the dependency Clean Architecture keeps out of that project.
    /// Since which transport carries the credential is an API-layer concern, the presence check
    /// belongs here, where both sources are visible.
    /// </remarks>
    private (string? Token, ActionResult? Failure) ResolveRefreshToken(RefreshTokenRequest request)
    {
        var cookieToken = Request.Cookies[WebAuthCookies.RefreshTokenCookieName];

        if (!string.IsNullOrEmpty(cookieToken))
        {
            // The browser attaches this cookie automatically, so without the double-submit check a
            // third-party page could trigger a refresh or logout on the user's behalf.
            if (!WebAuthCookies.HasValidCsrfToken(Request))
            {
                return (null, BadRequest(new { message = "طلب غير صالح: تحقق CSRF فشل." }));
            }

            return (cookieToken, null);
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return (null, BadRequest(new { message = "رمز التجديد مطلوب." }));
        }

        return (request.RefreshToken, null);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "اسم المستخدم أو كلمة المرور غير صحيحة"
            });
        }

        return RespondWithSession(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login/verify-2fa")]
    public async Task<ActionResult<LoginResponse>> VerifyTwoFactor(
        VerifyTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyTwoFactorAsync(
            request,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "الكود غير صحيح أو انتهت صلاحية الجلسة، سجل الدخول تاني."
            });
        }

        return RespondWithSession(result);
    }

    [Authorize]
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor(
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var result = await _authService.BeginTwoFactorSetupAsync(
            userId,
            cancellationToken);

        return Ok(result);
    }

    [Authorize]
    [HttpPost("2fa/enable")]
    public async Task<IActionResult> EnableTwoFactor(
        EnableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var succeeded = await _authService.EnableTwoFactorAsync(
            userId,
            request.Code,
            cancellationToken);

        if (!succeeded)
        {
            return BadRequest(new { message = "الكود غير صحيح." });
        }

        return NoContent();
    }

    [Authorize]
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(
        DisableTwoFactorRequest request,
        CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var succeeded = await _authService.DisableTwoFactorAsync(
            userId,
            request.Code,
            cancellationToken);

        if (!succeeded)
        {
            return BadRequest(new { message = "الكود غير صحيح." });
        }

        return NoContent();
    }

    [AllowAnonymous]
    // "auth-session", not "auth": this runs on every page load, so the strict
    // brute-force limit signed users out after a few reloads. See Program.cs.
    [EnableRateLimiting("auth-session")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var (token, failure) = ResolveRefreshToken(request);
        if (failure is not null)
        {
            return failure;
        }

        var result = await _authService.RefreshAsync(
            request with { RefreshToken = token ?? string.Empty },
            cancellationToken);

        if (result is null)
        {
            // The cookie is cleared on a rejected refresh so an expired or revoked token does not
            // sit in the browser causing every subsequent request to retry and fail.
            if (WebAuthCookies.UsesCookieTransport(Request))
            {
                WebAuthCookies.Clear(Response, _environment.IsDevelopment());
            }

            return Unauthorized(new
            {
                message = "جلسة الدخول منتهية، الرجاء تسجيل الدخول مرة أخرى"
            });
        }

        return RespondWithSession(result);
    }

    [AllowAnonymous]
    // "auth-session": a user who cannot sign out is an exposure, not something
    // worth throttling. See Program.cs.
    [EnableRateLimiting("auth-session")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var (token, failure) = ResolveRefreshToken(request);
        if (failure is not null)
        {
            return failure;
        }

        await _authService.RevokeAsync(
            request with { RefreshToken = token ?? string.Empty },
            cancellationToken);

        // Cleared unconditionally for cookie callers: even if the server-side revoke failed, the
        // browser must stop holding a credential the user has asked to give up.
        if (WebAuthCookies.UsesCookieTransport(Request))
        {
            WebAuthCookies.Clear(Response, _environment.IsDevelopment());
        }

        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(
            request,
            cancellationToken);

        // Always the same generic response whether or not the
        // username exists / has an email on file - never leak which
        // usernames are registered.
        return Ok(new
        {
            message = "لو الحساب موجود، هيوصله إيميل فيه رابط إعادة تعيين كلمة المرور."
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordWithTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(
            request,
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId = User.FindFirst(
                System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,

            displayName = User.Identity?.Name,

            role = User.FindFirst(
                System.Security.Claims.ClaimTypes.Role)?.Value,

            tenantId = User.FindFirst("tenant_id")?.Value,

            branchId = User.FindFirst("branch_id")?.Value
        });
    }
}