using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using PosFlow.Application.Branches;
using Xunit;

namespace PosFlow.Api.Tests;

public sealed class BranchesEndpointTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public BranchesEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task AuthenticateAsAdminAsync()
    {
        var token = await AuthHelper.LoginAsAdminAsync(_client);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    [Fact]
    public async Task GetBranches_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/branches");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateThenUpdateBranch_AsAdmin_Succeeds()
    {
        await AuthenticateAsAdminAsync();

        var createResponse = await _client.PostAsJsonAsync(
            "/api/branches",
            new CreateBranchRequest("فرع المعادي", "MAADI"));

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var branch = await createResponse.Content
            .ReadFromJsonAsync<BranchResponse>();

        Assert.True(branch!.IsActive);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/branches/{branch.Id}",
            new UpdateBranchRequest("فرع المعادي - الجديد", "MAADI2", false));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content
            .ReadFromJsonAsync<BranchResponse>();

        Assert.False(updated!.IsActive);
        Assert.Equal("MAADI2", updated.Code);
    }

    [Fact]
    public async Task CreateBranch_WithDuplicateCode_ReturnsConflict()
    {
        await AuthenticateAsAdminAsync();

        await _client.PostAsJsonAsync(
            "/api/branches",
            new CreateBranchRequest("فرع أ", "DUP"));

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/branches",
            new CreateBranchRequest("فرع ب", "DUP"));

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }
}
