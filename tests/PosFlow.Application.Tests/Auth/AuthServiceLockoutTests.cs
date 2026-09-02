using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PosFlow.Application.Auth;
using PosFlow.Application.Common;
using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Authentication;
using PosFlow.Infrastructure.Persistence;
using Xunit;

namespace PosFlow.Application.Tests.Auth;

/// <summary>
/// Covers account lockout at the AuthService level, bypassing HTTP.
/// The "auth" rate-limit policy caps /api/auth/login at 5
/// requests/minute per IP (see Program.cs), which would fire before
/// an endpoint-level test could ever reach the 5th failed attempt -
/// so this behaviour is only practical to test directly against the
/// service.
/// </summary>
public sealed class AuthServiceLockoutTests
{
    private const string SeededPassword = "Correct-Horse-1!";

    private static (AuthService Service, PosFlowDbContext DbContext, AppUser User) CreateSut(
        string databaseName)
    {
        var dbContext = TestDbContextFactory.Create(databaseName);

        var passwordHasher = new PasswordHasher<AppUser>();

        var tenant = new Tenant { Name = "Test Tenant" };
        dbContext.Tenants.Add(tenant);

        var user = new AppUser
        {
            TenantId = tenant.Id,
            Username = "cashier",
            NormalizedUsername = "CASHIER",
            DisplayName = "Test Cashier",
            PasswordHash = string.Empty
        };
        user.PasswordHash = passwordHasher.HashPassword(user, SeededPassword);

        dbContext.Users.Add(user);
        dbContext.SaveChanges();

        var jwtOptions = Options.Create(new JwtOptions
        {
            Key = "unit-test-signing-key-at-least-32-bytes-long!!",
            Issuer = "PosFlow.Tests",
            Audience = "PosFlow.Tests"
        });

        var configuration = new ConfigurationBuilder().Build();
        var emailSender = new NoOpEmailSender();

        var service = new AuthService(
            dbContext,
            passwordHasher,
            jwtOptions,
            emailSender,
            new NoOpEmailQueue(),
            configuration);

        return (service, dbContext, user);
    }

    [Fact]
    public async Task Login_AfterFiveConsecutiveFailures_LocksAccountEvenWithCorrectPassword()
    {
        var (service, _, user) = CreateSut(nameof(Login_AfterFiveConsecutiveFailures_LocksAccountEvenWithCorrectPassword));

        for (var i = 0; i < 5; i++)
        {
            var result = await service.LoginAsync(
                new LoginRequest(user.Username, "wrong-password"));

            Assert.Null(result);
        }

        // Correct password, but the account just crossed the lockout
        // threshold - must still be rejected, and with a distinct
        // signal (an exception, mapped to 403 at the API layer) rather
        // than the silent-null "bad credentials" result.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.LoginAsync(
                new LoginRequest(user.Username, SeededPassword)));
    }

    [Fact]
    public async Task Login_WithCorrectPasswordBeforeThreshold_ResetsFailedAttemptCounter()
    {
        var (service, dbContext, user) = CreateSut(nameof(Login_WithCorrectPasswordBeforeThreshold_ResetsFailedAttemptCounter));

        for (var i = 0; i < 4; i++)
        {
            await service.LoginAsync(
                new LoginRequest(user.Username, "wrong-password"));
        }

        var result = await service.LoginAsync(
            new LoginRequest(user.Username, SeededPassword));

        Assert.NotNull(result);

        var reloaded = await dbContext.Users.SingleAsync(x => x.Id == user.Id);
        Assert.Equal(0, reloaded.FailedLoginAttempts);
        Assert.Null(reloaded.LockoutEndUtc);
    }

    private sealed class NoOpEmailQueue : IBackgroundEmailQueue
    {
        public void Enqueue(QueuedEmail email) { }
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
