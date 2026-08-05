using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PosFlow.Application.Auth;

namespace PosFlow.Api.Controllers;

// The strict 5/min "auth" limiter is applied per-action below, only to
// endpoints an anonymous attacker could hammer to brute-force a
// credential (login, 2FA verification, password reset, refresh). The
// authenticated 2FA management endpoints (setup/enable/disable) are
// already gated by [Authorize] and covered by the global rate limiter
// instead - a legitimate logged-in user completing 2FA setup can
// easily make more than 5 requests/minute (setup, enable, a few
// retries) without that being remotely brute-force-shaped.
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService)
    : ControllerBase
{
    private readonly IAuthService _authService = authService;

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

        return Ok(result);
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

        return Ok(result);
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
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(
            request,
            cancellationToken);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "جلسة الدخول منتهية، الرجاء تسجيل الدخول مرة أخرى"
            });
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeAsync(
            request,
            cancellationToken);

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