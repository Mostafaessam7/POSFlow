using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OtpNet;
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
    private const int TwoFactorChallengeExpirationMinutes = 5;
    private const int MaxFailedLoginAttempts = 5;
    private const int LockoutDurationMinutes = 15;

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

        if (user.LockoutEndUtc is { } lockoutEnd && lockoutEnd > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException(
                $"الحساب مقفول مؤقتًا بسبب محاولات دخول فاشلة متكررة. حاول تاني بعد {LockoutDurationMinutes} دقيقة.");
        }

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
            {
                user.LockoutEndUtc = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                user.FailedLoginAttempts = 0;
            }

            user.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return null;
        }

        // A successful password check clears any prior failure streak,
        // whether or not it happened to also be the attempt that
        // crossed the lockout threshold moments earlier.
        user.FailedLoginAttempts = 0;
        user.LockoutEndUtc = null;

        if (verificationResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(user, request.Password);

            user.UpdatedAtUtc = DateTime.UtcNow;
        }

        if (user.TwoFactorEnabled)
        {
            return await IssueTwoFactorChallengeAsync(user, cancellationToken);
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<LoginResponse?> VerifyTwoFactorAsync(
        VerifyTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.ChallengeToken);

        var challenge = await _dbContext.TwoFactorChallenges
            .SingleOrDefaultAsync(
                x => x.ChallengeTokenHash == tokenHash,
                cancellationToken);

        if (challenge is null || !challenge.IsActive)
        {
            return null;
        }

        var user = await _dbContext.Users
            .SingleOrDefaultAsync(
                x => x.Id == challenge.UserId && x.IsActive,
                cancellationToken);

        if (user is null ||
            !user.TwoFactorEnabled ||
            string.IsNullOrEmpty(user.TwoFactorSecret) ||
            !VerifyTotpCode(user.TwoFactorSecret, request.Code))
        {
            return null;
        }

        challenge.UsedAtUtc = DateTime.UtcNow;

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<TwoFactorSetupResponse> BeginTwoFactorSetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var secretKey = Base32Encoding.ToString(secretBytes);

        user.TwoFactorSecret = secretKey;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var issuer = Uri.EscapeDataString("PosFlow");
        var label = Uri.EscapeDataString(user.Username);
        var otpAuthUri =
            $"otpauth://totp/{issuer}:{label}?secret={secretKey}&issuer={issuer}&digits=6&period=30";

        return new TwoFactorSetupResponse(secretKey, otpAuthUri);
    }

    public async Task<bool> EnableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        if (string.IsNullOrEmpty(user.TwoFactorSecret) ||
            !VerifyTotpCode(user.TwoFactorSecret, code))
        {
            return false;
        }

        user.TwoFactorEnabled = true;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DisableTwoFactorAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .SingleAsync(x => x.Id == userId, cancellationToken);

        if (!user.TwoFactorEnabled ||
            string.IsNullOrEmpty(user.TwoFactorSecret) ||
            !VerifyTotpCode(user.TwoFactorSecret, code))
        {
            return false;
        }

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static bool VerifyTotpCode(string base32Secret, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));

        // ±1 step (30s) window absorbs minor clock drift between the
        // user's authenticator app and this server, same as every
        // mainstream TOTP implementation.
        return totp.VerifyTotp(
            code.Trim(),
            out _,
            new VerificationWindow(1, 1));
    }

    private async Task<LoginResponse> IssueTwoFactorChallengeAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var rawChallengeToken = GenerateSecureRandomToken();

        _dbContext.TwoFactorChallenges.Add(new TwoFactorChallenge
        {
            UserId = user.Id,
            ChallengeTokenHash = HashToken(rawChallengeToken),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(
                TwoFactorChallengeExpirationMinutes)
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LoginResponse(
            TwoFactorRequired: true,
            ChallengeToken: rawChallengeToken,
            AccessToken: null,
            ExpiresAtUtc: null,
            RefreshToken: null,
            UserId: null,
            TenantId: null,
            BranchId: null,
            DisplayName: null,
            Role: null);
    }

    public async Task<LoginResponse?> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        // No credential supplied at all. The API layer resolves cookie-or-body before calling here,
        // so this is the "neither was present" case and is the same outcome as an unknown token.
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return null;
        }

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
        // Nothing to revoke without a credential; treat it as a no-op rather than an error, so a
        // logout with an already-cleared cookie still succeeds instead of failing the sign-out.
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return;
        }

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
            TwoFactorRequired: false,
            ChallengeToken: null,
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
