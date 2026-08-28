using System.Net;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// Routing breaks silently: if the versioning convention stops applying, nothing fails at build
/// time and no other test notices — only the frontend would, by 404ing on every call.
///
/// These pin the two properties the change rests on: the existing unversioned routes still work
/// (the Angular client depends on them), and the versioned routes exist.
/// </summary>
public sealed class ApiVersioningTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApiVersioningTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    /// <summary>
    /// The client issues same-origin relative requests to /api/products, /api/orders and so on, so
    /// losing these routes would break every call in the application.
    /// </summary>
    [Theory]
    [InlineData("/api/auth/login")]
    public async Task Legacy_unversioned_routes_still_resolve(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });

        // Asserting the route *exists*, not that an empty body succeeds. Anything other than 404
        // means routing found the endpoint and the request failed later, on its merits.
        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    public async Task Versioned_routes_resolve(string path)
    {
        var response = await _client.PostAsJsonAsync(path, new { });

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Both_forms_reach_the_same_endpoint()
    {
        // Identical results for identical input is what makes the versioned route an addition
        // rather than a second endpoint that drifts.
        var legacy = await _client.PostAsJsonAsync("/api/auth/login", new { });
        var versioned = await _client.PostAsJsonAsync("/api/v1/auth/login", new { });

        Assert.Equal(legacy.StatusCode, versioned.StatusCode);
    }

    [Fact]
    public async Task An_undefined_version_is_rejected_rather_than_served()
    {
        // If v9 worked, the version segment would be decoration rather than a contract.
        var response = await _client.PostAsJsonAsync("/api/v9/auth/login", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
