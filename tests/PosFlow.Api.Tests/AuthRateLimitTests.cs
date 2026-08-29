using System.Net;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// Pins the split between the two auth rate-limit policies.
///
/// Every auth endpoint shared one 5-requests-per-minute policy. A browser test measured the
/// consequence: log in, reload the page four times, and the fourth reload gets 429 -- the client
/// treats a failed refresh as "no session" and signs the user out. Reloading four times in a
/// minute is not abuse, and the limit is keyed per IP, so a shop's terminals behind one public
/// address spend each other's budget.
///
/// Both halves need pinning. Relaxing refresh is only correct while login stays strict, so the
/// second test here is the one that stops this being remembered as "the limit was too low".
///
/// xUnit builds a new instance of this class per test method, so each test gets its own
/// PosFlowApiFactory and therefore its own rate-limiter state. That isolation is what makes it
/// safe to deliberately exhaust a limit here.
/// </summary>
public sealed class AuthRateLimitTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthRateLimitTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Refresh_is_not_throttled_at_the_brute_force_limit()
    {
        // Eight is past the strict 5/min budget. These get 400 (no token supplied), which is fine
        // -- the assertion is about 429 specifically, not about the refresh succeeding.
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 8; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { });
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Logout_is_not_throttled_at_the_brute_force_limit()
    {
        // A user who cannot sign out is an exposure, not a protected resource.
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 8; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/logout", new { });
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Login_is_still_throttled_after_five_attempts()
    {
        // The half that must not be relaxed: login takes a guessable secret, so this is the real
        // brute-force surface the strict policy exists for.
        var statuses = new List<HttpStatusCode>();

        for (var i = 0; i < 7; i++)
        {
            var response = await _client.PostAsJsonAsync(
                "/api/auth/login",
                new { username = "admin", password = "definitely-not-the-password" });

            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
