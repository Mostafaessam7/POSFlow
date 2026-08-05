using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Auth;
using PosFlow.Application.Common;
using PosFlow.Application.Shifts;
using PosFlow.Application.Users;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class ShiftsBranchHistoryTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public ShiftsBranchHistoryTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task BranchHistory_AsCashier_ReturnsForbidden()
    {
        var adminLogin = await LoginAsync(
            AuthHelper.SeededAdminUsername,
            AuthHelper.SeededAdminPassword);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminLogin.AccessToken);

        await _client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                "cashier3", "كاشير", null, "Cashier@123", "Cashier", adminLogin.BranchId));

        var cashierLogin = await LoginAsync("cashier3", "Cashier@123");

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cashierLogin.AccessToken);

        var response = await _client.GetAsync("/api/shifts/branch-history");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BranchHistory_AsAdmin_ShowsOtherCashiersShiftsWithTheirName()
    {
        var adminLogin = await LoginAsync(
            AuthHelper.SeededAdminUsername,
            AuthHelper.SeededAdminPassword);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminLogin.AccessToken);

        // Same branch as the admin - required for the branch-history
        // filter to pick the cashier's shift up.
        await _client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest(
                "cashier4", "كاشير رابع", null, "Cashier@123", "Cashier", adminLogin.BranchId));

        var cashierLogin = await LoginAsync("cashier4", "Cashier@123");

        using var cashierClient = _factory.CreateClient();

        cashierClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", cashierLogin.AccessToken);

        var openShiftResponse = await cashierClient.PostAsJsonAsync(
            "/api/shifts/open",
            new OpenShiftRequest(50m));

        openShiftResponse.EnsureSuccessStatusCode();

        var branchHistoryResponse = await _client.GetAsync(
            "/api/shifts/branch-history");

        Assert.Equal(HttpStatusCode.OK, branchHistoryResponse.StatusCode);

        var history = await branchHistoryResponse.Content
            .ReadFromJsonAsync<PagedResult<ShiftResponse>>();

        Assert.Contains(
            history!.Items,
            s => s.CashierName == "كاشير رابع");
    }

    private async Task<LoginResponse> LoginAsync(
        string username,
        string password)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(username, password));

        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<LoginResponse>())!;
    }
}
