using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OtpNet;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Auth;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class TwoFactorAuthTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public TwoFactorAuthTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task FullTwoFactorLifecycle_SetupEnableLoginDisable()
    {
        var accessToken = await AuthHelper.LoginAsAdminAsync(_client);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        // 1) Begin setup - get a real TOTP secret back.
        var setupResponse = await _client.PostAsync(
            "/api/auth/2fa/setup", content: null);

        setupResponse.EnsureSuccessStatusCode();

        var setup = await setupResponse.Content
            .ReadFromJsonAsync<TwoFactorSetupResponse>();

        Assert.NotNull(setup);
        Assert.NotEmpty(setup!.SecretKey);

        var totp = new Totp(Base32Encoding.ToBytes(setup.SecretKey));

        // 2) Enable using a real generated code.
        var enableResponse = await _client.PostAsJsonAsync(
            "/api/auth/2fa/enable",
            new EnableTwoFactorRequest(totp.ComputeTotp()));

        Assert.Equal(HttpStatusCode.NoContent, enableResponse.StatusCode);

        // 3) A fresh login now returns a 2FA challenge, not tokens.
        _client.DefaultRequestHeaders.Authorization = null;

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthHelper.SeededAdminUsername,
                AuthHelper.SeededAdminPassword));

        loginResponse.EnsureSuccessStatusCode();

        var challenge = await loginResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(challenge);
        Assert.True(challenge!.TwoFactorRequired);
        Assert.Null(challenge.AccessToken);
        Assert.NotNull(challenge.ChallengeToken);

        // 4) Completing the challenge with a real code yields real tokens.
        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/login/verify-2fa",
            new VerifyTwoFactorRequest(
                challenge.ChallengeToken!,
                totp.ComputeTotp()));

        verifyResponse.EnsureSuccessStatusCode();

        var verified = await verifyResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.False(verified!.TwoFactorRequired);
        Assert.NotNull(verified.AccessToken);

        // 5) Disable, using a fresh code (re-authenticate with the new token).
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", verified.AccessToken);

        var disableResponse = await _client.PostAsJsonAsync(
            "/api/auth/2fa/disable",
            new DisableTwoFactorRequest(totp.ComputeTotp()));

        Assert.Equal(HttpStatusCode.NoContent, disableResponse.StatusCode);

        // 6) Login now succeeds directly again, no challenge.
        var finalLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthHelper.SeededAdminUsername,
                AuthHelper.SeededAdminPassword));

        var finalLogin = await finalLoginResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        Assert.False(finalLogin!.TwoFactorRequired);
        Assert.NotNull(finalLogin.AccessToken);
    }

    [Fact]
    public async Task VerifyTwoFactor_WithWrongCode_ReturnsUnauthorized()
    {
        var accessToken = await AuthHelper.LoginAsAdminAsync(_client);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        var setup = await (await _client.PostAsync(
            "/api/auth/2fa/setup", content: null))
            .Content.ReadFromJsonAsync<TwoFactorSetupResponse>();

        var totp = new Totp(Base32Encoding.ToBytes(setup!.SecretKey));

        await _client.PostAsJsonAsync(
            "/api/auth/2fa/enable",
            new EnableTwoFactorRequest(totp.ComputeTotp()));

        _client.DefaultRequestHeaders.Authorization = null;

        var challenge = await (await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthHelper.SeededAdminUsername,
                AuthHelper.SeededAdminPassword)))
            .Content.ReadFromJsonAsync<LoginResponse>();

        var wrongCodeResponse = await _client.PostAsJsonAsync(
            "/api/auth/login/verify-2fa",
            new VerifyTwoFactorRequest(challenge!.ChallengeToken!, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongCodeResponse.StatusCode);
    }
}
