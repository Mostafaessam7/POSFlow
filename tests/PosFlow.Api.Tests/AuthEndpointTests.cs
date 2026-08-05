using System.Net;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Auth;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class AuthEndpointTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsAccessAndRefreshTokens()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthHelper.SeededAdminUsername,
                AuthHelper.SeededAdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(AuthHelper.SeededAdminUsername, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("no-such-user", "whatever123"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyUsername_ReturnsBadRequestFromValidationFilter()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("", "somepassword"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithGarbageToken_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest("not-a-real-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshFlow_RotatesToken_AndOldTokenNoLongerWorks()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(
                AuthHelper.SeededAdminUsername,
                AuthHelper.SeededAdminPassword));

        var login = await loginResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        var firstRefresh = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login!.RefreshToken!));

        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        // The refresh token is single-use (rotated) - reusing the same
        // one a second time must now be rejected.
        var reusedRefresh = await _client.PostAsJsonAsync(
            "/api/auth/refresh",
            new RefreshTokenRequest(login.RefreshToken!));

        Assert.Equal(HttpStatusCode.Unauthorized, reusedRefresh.StatusCode);
    }
}
