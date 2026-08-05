using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PosFlow.Application.Auth;
using PosFlow.Application.Common;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Infrastructure.Authentication;

public sealed class AuthService(
    PosFlowDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IEmailSender emailSender,
    IConfiguration configuration)
    : IAuthService
{
    private const int PasswordResetTokenExpirationMinutes = 30;

    private readonly PosFlowDbContext _dbContext = dbContext;
    private readonly IPasswordHasher<AppUser> _passwordHasher = passwordHasher;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly IEmailSender _emailSender = emailSender;
    private readonly IConfiguration _configuration = configuration;

    public async Task<LoginResponse?> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username
            .Trim()
            .ToUpperInvariant();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.NormalizedUsername == normalizedUsername &&
                     x.IsActive,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verificationResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(user, request.Password);

            user.UpdatedAtUtc = DateTime.UtcNow;
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<LoginResponse?> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            return null;
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == existingToken.UserId &&
                     x.IsActive,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        // Rotate: the old refresh token is single-use. Revoke it and
        // hand back a brand new access + refresh token pair.
        existingToken.RevokedAtUtc = DateTime.UtcNow;

        var response = await IssueTokensAsync(user, cancellationToken, existingToken);

        return response;
    }

    public async Task RevokeAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (existingToken is null || !existingToken.IsActive)
        {
            return;
        }

        existingToken.RevokedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username
            .Trim()
            .ToUpperInvariant();

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.NormalizedUsername == normalizedUsername &&
                     x.IsActive,
                cancellationToken);

        // Deliberately silent no-op if the username doesn't exist or
        // has no email on file - the controller always returns the
        // same generic response either way, so this never leaks
        // which usernames are registered.
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var rawToken = GenerateSecureRandomToken();

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(
                PasswordResetTokenExpirationMinutes)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var frontendBaseUrl = _configuration["App:FrontendBaseUrl"]
            ?.TrimEnd('/')
            ?? "http://localhost:4200";

        var resetLink =
            $"{frontendBaseUrl}/reset-password?token={Uri.EscapeDataString(rawToken)}";

        await _emailSender.SendAsync(
            user.Email,
            "إعادة تعيين كلمة المرور - PosFlow",
            $"مرحبًا {user.DisplayName}،\n\n" +
            $"اضغط على الرابط ده لإعادة تعيين كلمة المرور (صالح لمدة {PasswordResetTokenExpirationMinutes} دقيقة):\n" +
            $"{resetLink}\n\n" +
            "لو ما طلبتش ده، تجاهل الإيميل ده.",
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordWithTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token);

        var resetToken = await _dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(
                x => x.TokenHash == tokenHash,
                cancellationToken);

        if (resetToken is null || !resetToken.IsActive)
        {
            throw new UnauthorizedAccessException(
                "رابط إعادة التعيين غير صالح أو منتهي الصلاحية.");
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == resetToken.UserId,
                cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(
                "رابط إعادة التعيين غير صالح أو منتهي الصلاحية.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.NewPassword);

        user.UpdatedAtUtc = DateTime.UtcNow;
        resetToken.UsedAtUtc = DateTime.UtcNow;

        // Reset means "I might have been compromised" - force
        // re-login everywhere by revoking every other active session.
        var activeRefreshTokens = await _dbContext.RefreshTokens
            .Where(
                x => x.UserId == user.Id &&
                     x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeRefreshTokens)
        {
            refreshToken.RevokedAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateSecureRandomToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private async Task<LoginResponse> IssueTokensAsync(
        AppUser user,
        CancellationToken cancellationToken,
        RefreshToken? replacedToken = null)
    {
        var expiresAtUtc = DateTime.UtcNow
            .AddMinutes(_jwtOptions.ExpirationMinutes);

        var claims = new List<Claim>
        {
            new(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),

            new(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.DisplayName),

            new(
                ClaimTypes.Role,
                user.Role.ToString()),

            new(
                "tenant_id",
                user.TenantId.ToString())
        };

        if (user.BranchId.HasValue)
        {
            claims.Add(
                new Claim(
                    "branch_id",
                    user.BranchId.Value.ToString()));
        }

        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtOptions.Key));

        var credentials = new SigningCredentials(
            signingKey,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var accessToken =
            new JwtSecurityTokenHandler().WriteToken(token);

        var rawRefreshToken = GenerateSecureRandomToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(
                _jwtOptions.RefreshTokenExpirationDays)
        };

        _dbContext.RefreshTokens.Add(refreshToken);

        if (replacedToken is not null)
        {
            replacedToken.ReplacedByTokenId = refreshToken.Id;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            AccessToken: accessToken,
            ExpiresAtUtc: expiresAtUtc,
            RefreshToken: rawRefreshToken,
            UserId: user.Id,
            TenantId: user.TenantId,
            BranchId: user.BranchId,
            DisplayName: user.DisplayName,
            Role: user.Role.ToString());
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
