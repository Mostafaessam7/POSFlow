using System.Net.Http.Json;
using PosFlow.Api.Middleware;
using PosFlow.Api.Tests.TestHelpers;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// Every response must carry a correlation id, and a caller-supplied one must be honoured only
/// when it is safe to write into a log.
/// </summary>
/// <remarks>
/// The value reaches log output, so an unvalidated header would let any caller forge log entries
/// by embedding newlines — the tests below are as much about that as about the happy path.
/// </remarks>
public sealed class CorrelationIdTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public CorrelationIdTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(
            response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values),
            $"No {CorrelationIdMiddleware.HeaderName} on the response. Without one there is nothing to quote in a bug report.");

        Assert.False(string.IsNullOrWhiteSpace(values!.Single()));
    }

    [Fact]
    public async Task Two_requests_get_different_ids()
    {
        // A constant id would be worse than none: it would look like correlation while grouping
        // every request in the system together.
        var first = await GetCorrelationIdAsync("/health");
        var second = await GetCorrelationIdAsync("/health");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task A_safe_caller_supplied_id_is_honoured()
    {
        // The point of accepting one at all: a caller can tie its own logs to ours.
        const string supplied = "checkout-terminal-3-a1b2c3";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

        var response = await _client.SendAsync(request);

        Assert.Equal(supplied, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Theory]
    // Newlines are the dangerous case: this value is written to the log, so accepting one would
    // let a caller append fabricated log lines of their own choosing.
    [InlineData("bad\nid")]
    [InlineData("bad\r\nid")]
    // Anything that could confuse a log parser or a downstream consumer.
    [InlineData("id with spaces")]
    [InlineData("id\"with\"quotes")]
    [InlineData("{\"json\":\"injection\"}")]
    // Unbounded length would let one request bloat the log arbitrarily.
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task An_unsafe_caller_supplied_id_is_replaced_not_echoed(string unsafeValue)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        // TryAddWithoutValidation: HttpClient itself rejects newline-carrying header values, and
        // the point here is to test the server's own validation rather than the client's.
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, unsafeValue);

        var response = await _client.SendAsync(request);
        var returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();

        Assert.NotEqual(unsafeValue, returned);

        // Replaced with a generated id, not merely trimmed — a sanitised fragment of attacker
        // input is still attacker input.
        Assert.Matches("^[0-9a-f]{32}$", returned);
    }

    private async Task<string> GetCorrelationIdAsync(string path)
    {
        var response = await _client.GetAsync(path);
        return response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();
    }
}
