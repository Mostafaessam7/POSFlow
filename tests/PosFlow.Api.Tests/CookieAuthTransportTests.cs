using System.Net;
using System.Net.Http.Json;
using PosFlow.Api.Tests.TestHelpers;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// Covers the HttpOnly-cookie auth transport.
///
/// The property that matters most is the negative one: after a cookie login, the refresh token must
/// NOT be in the JSON body. Returning it in both places would leave it readable by any script on
/// the page and make the whole change cosmetic — and that is a mistake nothing else would catch,
/// because everything would still work.
/// </summary>
public sealed class CookieAuthTransportTests : IDisposable
{
    private readonly PosFlowApiFactory _factory = new();
    private readonly HttpClient _client;

    public CookieAuthTransportTests()
    {
        // Cookies are handled manually so each assertion can inspect the exact Set-Cookie headers
        // rather than trusting a handler to have done the right thing.
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<HttpResponseMessage> LoginAsync(bool cookieTransport)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username = "admin", password = "Admin@123" }),
        };

        if (cookieTransport)
        {
            request.Headers.Add("X-Auth-Transport", "cookie");
        }

        return await _client.SendAsync(request);
    }

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    [Fact]
    public async Task Body_transport_is_unchanged_when_the_header_is_absent()
    {
        // Non-browser callers - the terminal agent, scripts, the other tests in this suite - must
        // keep working exactly as before. This change is additive.
        var response = await LoginAsync(cookieTransport: false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Fail("Login failed, so the assertions below never ran. Fix the seeded admin rather than letting this test pass silently.");
        }

        Assert.Empty(SetCookies(response).Where(c => c.StartsWith("posflow_rt", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Cookie_login_sets_an_HttpOnly_refresh_cookie()
    {
        var response = await LoginAsync(cookieTransport: true);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Fail("Login failed, so the assertions below never ran. Fix the seeded admin rather than letting this test pass silently.");
        }

        var refreshCookie = SetCookies(response)
            .FirstOrDefault(c => c.StartsWith("posflow_rt", StringComparison.Ordinal));

        Assert.NotNull(refreshCookie);

        // HttpOnly is the entire point: an XSS payload that has hooked fetch/XHR still cannot read
        // this value.
        Assert.Contains("httponly", refreshCookie, StringComparison.OrdinalIgnoreCase);

        // Scoped to the auth endpoints - the refresh token has no reason to ride along on every
        // product and order request.
        Assert.Contains("path=/api/auth", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cookie_login_does_not_also_return_the_refresh_token_in_the_body()
    {
        // The regression that would make this change pointless while appearing to work.
        var response = await LoginAsync(cookieTransport: true);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Fail("Login failed, so the assertions below never ran. Fix the seeded admin rather than letting this test pass silently.");
        }

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("\"refreshToken\":\"", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Csrf_cookie_is_readable_by_script_unlike_the_refresh_cookie()
    {
        // Deliberately NOT HttpOnly: the SPA has to read it and echo it back in a header. That
        // asymmetry is what makes double-submit work.
        var response = await LoginAsync(cookieTransport: true);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Assert.Fail("Login failed, so the assertions below never ran. Fix the seeded admin rather than letting this test pass silently.");
        }

        var csrfCookie = SetCookies(response)
            .FirstOrDefault(c => c.StartsWith("XSRF-TOKEN", StringComparison.Ordinal));

        Assert.NotNull(csrfCookie);
        Assert.DoesNotContain("httponly", csrfCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_with_a_cookie_but_no_CSRF_header_is_rejected()
    {
        // Without this, moving the credential into a cookie would trade XSS exposure for CSRF
        // exposure - the browser attaches the cookie to a third-party form post automatically.
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = "" }),
        };
        request.Headers.Add("Cookie", "posflow_rt=some-token; XSRF-TOKEN=abc123");
        // No X-XSRF-TOKEN header - this is the forged-request shape.

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_a_mismatched_CSRF_header_is_rejected()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken = "" }),
        };
        request.Headers.Add("Cookie", "posflow_rt=some-token; XSRF-TOKEN=abc123");
        request.Headers.Add("X-XSRF-TOKEN", "a-different-value");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_any_token_is_a_clean_400_not_a_500()
    {
        // The FluentValidation NotEmpty rule used to cover this; it had to move to the controller
        // because a validator in the Application layer cannot see cookies. This pins that the
        // behaviour survived the move.
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
