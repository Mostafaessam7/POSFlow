using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Auth;
using PosFlow.Application.Products;
using PosFlow.Application.Users;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class AuthorizationTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthorizationTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetProducts_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithValidAdminToken_ReturnsOk()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetUsers_AsCashier_ReturnsForbidden()
    {
        // Admin-only endpoint (/api/users). A cashier holds a
        // perfectly valid token, just not the right role.
        var adminToken = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var createUserResponse = await _client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                "cashier1", "كاشير تجريبي", null, "Cashier@123", "Cashier", null));

        createUserResponse.EnsureSuccessStatusCode();

        var cashierLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("cashier1", "Cashier@123"));

        var cashierLogin = await cashierLoginResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cashierLogin!.AccessToken);

        var getUsersResponse = await _client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, getUsersResponse.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_AsCashier_ReturnsForbidden()
    {
        var adminToken = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminToken);

        var createUserResponse = await _client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                "cashier2", "كاشير آخر", null, "Cashier@123", "Cashier", null));

        createUserResponse.EnsureSuccessStatusCode();

        var cashierLoginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("cashier2", "Cashier@123"));

        var cashierLogin = await cashierLoginResponse.Content
            .ReadFromJsonAsync<LoginResponse>();

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cashierLogin!.AccessToken);

        var createProductResponse = await _client.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest("منتج", null, null, 10m, null, false, 0));

        Assert.Equal(HttpStatusCode.Forbidden, createProductResponse.StatusCode);
    }
}
