using System.Net.Http.Json;
using PosFlow.Application.Auth;

namespace PosFlow.Api.Tests.TestHelpers;

public static class AuthHelper
{
    // Matches DatabaseSeeder - the only account guaranteed to exist
    // on a freshly seeded database.
    public const string SeededAdminUsername = "admin";
    public const string SeededAdminPassword = "Admin@123";

    public static async Task<string> LoginAsAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(SeededAdminUsername, SeededAdminPassword));

        response.EnsureSuccessStatusCode();

        var result = await response.Content
            .ReadFromJsonAsync<LoginResponse>();

        return result!.AccessToken;
    }
}
