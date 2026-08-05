using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PosFlow.Application.Auth;

namespace PosFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    IAuthService authService)
    : ControllerBase
{
    private readonly IAuthService _authService = authService;

    [AllowAnonymous]
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