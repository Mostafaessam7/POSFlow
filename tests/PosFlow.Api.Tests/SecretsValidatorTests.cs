using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using PosFlow.Api.Configuration;
using Xunit;

namespace PosFlow.Api.Tests;

/// <summary>
/// These tests exist because the guard they cover is only ever exercised on a path nobody watches:
/// a production start-up. If it silently stopped working, the symptom would be an authentication
/// bypass in a live deployment, not a failing build. So the real placeholder shipped in
/// appsettings.json is pinned here by value.
/// </summary>
public class SecretsValidatorTests
{
    /// <summary>The exact key checked into appsettings.json — 60 characters, so length checks pass it.</summary>
    private const string ShippedPlaceholderKey = "PosFlow-Development-Key-Change-Me-2026-Minimum-32-Characters";

    private const string RealisticKey = "b7Kq2vXm9Rt4Zp8Lw6Cy3Nd5Hf1Js0Gu7Ae4Bo2Qi9Vx6Mz";

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment Env(string name) => new StubEnvironment(name);

    private static Dictionary<string, string?> ValidBaseline() => new()
    {
        ["Jwt:Key"] = RealisticKey,
        ["ConnectionStrings:DefaultConnection"] = "Server=db;Database=PosFlow;User Id=sa;Password=r3al-Pw!;TrustServerCertificate=True",
    };

    [Fact]
    public void Rejects_the_placeholder_key_that_ships_in_appsettings()
    {
        // The regression that matters: this key is 60 characters, so `Key.Length < 32` accepts it and
        // the API would start and sign real tokens with a key published in the repository.
        var config = Config(new Dictionary<string, string?>(ValidBaseline())
        {
            ["Jwt:Key"] = ShippedPlaceholderKey,
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Production")));

        Assert.Contains("Jwt:Key", ex.Message);
    }

    [Fact]
    public void The_shipped_placeholder_would_pass_a_length_only_check()
    {
        // Pins the premise of the test above. If someone shortens the placeholder, the length check
        // starts catching it and this test fails loudly rather than the suite quietly losing its point.
        Assert.True(ShippedPlaceholderKey.Length >= 32);
    }

    [Theory]
    [InlineData("CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARACTERS")]
    [InlineData("my-development-key-that-is-definitely-long-enough-to-pass")]
    [InlineData("REPLACE_ME_WITH_SOMETHING_REAL_BEFORE_DEPLOYING_TO_PROD")]
    [InlineData("your-secret-key-goes-right-here-and-is-long-enough-ok")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void Rejects_placeholder_shaped_keys_it_has_never_seen_before(string key)
    {
        // The point of pattern matching over a known-value list: none of these are the shipped
        // placeholder, and all of them are things a person actually types.
        var config = Config(new Dictionary<string, string?>(ValidBaseline()) { ["Jwt:Key"] = key });

        Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Production")));
    }

    [Fact]
    public void Rejects_a_long_key_with_almost_no_variation()
    {
        var config = Config(new Dictionary<string, string?>(ValidBaseline())
        {
            ["Jwt:Key"] = new string('a', 64),
        });

        Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Production")));
    }

    [Fact]
    public void Rejects_a_missing_key()
    {
        var config = Config(new Dictionary<string, string?>(ValidBaseline()) { ["Jwt:Key"] = null });

        Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Production")));
    }

    [Fact]
    public void Reports_every_problem_at_once_rather_than_the_first()
    {
        // Fixing one placeholder, redeploying, and hitting the next one is a miserable loop to be in
        // during an outage window.
        var config = Config(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = ShippedPlaceholderKey,
            ["ConnectionStrings:DefaultConnection"] = "Server=db;Password=your-password-here;",
        });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Production")));

        Assert.Contains("Jwt:Key", ex.Message);
        Assert.Contains("DefaultConnection", ex.Message);
    }

    [Fact]
    public void Allows_the_placeholders_through_in_Development()
    {
        // Clone-and-run has to keep working; that is what the checked-in defaults are for.
        var config = Config(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = ShippedPlaceholderKey,
            ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=PosFlowDb;Trusted_Connection=True;",
        });

        SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("Development"));
    }

    [Fact]
    public void Treats_an_unrecognised_environment_name_as_real()
    {
        // Anything that is not Development gets the strict path — a bespoke environment name like
        // "QA" or a typo'd "Prod" must not silently opt out of the check.
        var config = Config(new Dictionary<string, string?>(ValidBaseline())
        {
            ["Jwt:Key"] = ShippedPlaceholderKey,
        });

        Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(config, Env("QA")));
    }

    [Fact]
    public void Accepts_a_fully_configured_production_setup()
    {
        SecretsValidator.EnsureProductionSecretsAreConfigured(Config(ValidBaseline()), Env("Production"));
    }

    [Fact]
    public void Ignores_optional_integrations_that_are_not_configured_at_all()
    {
        // Running without SMTP is supported: the sender falls back to logging. Absent must not be
        // conflated with placeholder.
        var values = ValidBaseline();
        values["Smtp:Password"] = "";

        SecretsValidator.EnsureProductionSecretsAreConfigured(Config(values), Env("Production"));
    }

    [Fact]
    public void Rejects_an_optional_integration_left_on_its_placeholder()
    {
        var values = ValidBaseline();
        values["Smtp:Password"] = "CHANGE_THIS_TO_YOUR_SMTP_PASSWORD";

        Assert.Throws<InvalidOperationException>(() =>
            SecretsValidator.EnsureProductionSecretsAreConfigured(Config(values), Env("Production")));
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "PosFlow.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
