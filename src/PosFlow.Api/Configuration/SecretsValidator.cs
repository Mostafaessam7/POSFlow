namespace PosFlow.Api.Configuration;

/// <summary>
/// Refuses to start outside Development when a checked-in placeholder secret is still in effect.
///
/// The dev-time defaults in <c>appsettings.json</c> exist so a new contributor can clone and run, which
/// means they are published in the repository by design. The danger is that they are also *valid* — the
/// JWT key that ships here is 60 characters, so the existing <c>Key.Length &lt; 32</c> check accepts it and
/// the API starts normally, signing real tokens with a key anyone reading the repo already has. A
/// deployment that misses one environment variable gets a silent authentication bypass with no error and
/// no log line.
///
/// Matching is done on placeholder *patterns* rather than a list of exact known strings: a hardcoded list
/// only catches the placeholders somebody remembered to enumerate, and silently stops protecting the
/// moment a new one is written. Length is treated as necessary but not sufficient, and paired with a
/// cheap distinct-character check so a long-but-degenerate key ("aaaa…") is rejected too.
/// </summary>
public static class SecretsValidator
{
    /// <summary>
    /// Substrings that mark a value as a stand-in rather than a real secret. Matched case-insensitively
    /// anywhere in the value, so "PosFlow-Development-Key-Change-Me-2026-…" is caught by both
    /// "change-me" and "development-key".
    /// </summary>
    private static readonly string[] PlaceholderMarkers =
    [
        "change_this", "change-this", "changethis",
        "change_me", "change-me", "changeme",
        "replace_me", "replace-me", "replaceme",
        "your-", "your_", "yourpassword", "your password",
        "placeholder", "example.com", "sample", "dummy", "todo",
        "development-key", "development_key", "dev-key", "dev_key",
        "test-key", "test_key", "secret123", "password123",
        "xxxx", "insert_", "insert-",
    ];

    /// <summary>Below this, a signing key is brute-forceable regardless of content.</summary>
    private const int MinimumSigningKeyLength = 32;

    /// <summary>
    /// A 60-character key of one repeated character is long but worthless. This is a crude entropy
    /// proxy, not a real strength measure — it exists to catch obvious filler, not to grade keys.
    /// </summary>
    private const int MinimumDistinctCharacters = 12;

    public static void EnsureProductionSecretsAreConfigured(IConfiguration configuration, IHostEnvironment environment)
    {
        // Development keeps the checked-in defaults: that is what they are for. Everything else —
        // Staging, Production, and any custom environment name — is treated as real.
        if (environment.IsDevelopment())
        {
            return;
        }

        var problems = new List<string>();

        var jwtKey = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            problems.Add("Jwt:Key is not set. Supply it via the Jwt__Key environment variable or Key Vault.");
        }
        else
        {
            if (jwtKey.Length < MinimumSigningKeyLength)
            {
                problems.Add($"Jwt:Key is shorter than {MinimumSigningKeyLength} characters.");
            }

            if (jwtKey.Distinct().Count() < MinimumDistinctCharacters)
            {
                problems.Add("Jwt:Key has too little variation to be a real signing key.");
            }

            if (LooksLikePlaceholder(jwtKey))
            {
                problems.Add(
                    "Jwt:Key is still a checked-in placeholder. This value is published in the repository, " +
                    "so anyone could forge tokens for any user. Supply a real key via the Jwt__Key " +
                    "environment variable or Key Vault.");
            }
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            problems.Add("ConnectionStrings:DefaultConnection is not set. Supply it via the ConnectionStrings__DefaultConnection environment variable or Key Vault.");
        }
        else if (LooksLikePlaceholder(connectionString))
        {
            problems.Add("ConnectionStrings:DefaultConnection still contains a placeholder value.");
        }

        // Optional integrations: only validated when configured at all, since running without email
        // is a supported deployment (the sender falls back to logging).
        RequireRealIfPresent(configuration, "Smtp:Password", problems);
        RequireRealIfPresent(configuration, "Smtp:Username", problems);

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Refusing to start in '{environment.EnvironmentName}' with placeholder secrets still in effect:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Select(p => "  - " + p))
                + Environment.NewLine
                + "See deploy/README.md for how each value should be supplied.");
        }
    }

    private static void RequireRealIfPresent(IConfiguration configuration, string key, List<string> problems)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value) && LooksLikePlaceholder(value))
        {
            problems.Add($"{key} is still a checked-in placeholder. Supply it via the {key.Replace(':', '_').Replace("_", "__")} environment variable, or remove the section entirely to disable the integration.");
        }
    }

    private static bool LooksLikePlaceholder(string value) =>
        PlaceholderMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
