using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using PosFlow.Api.Authorization;
using PosFlow.Api.Configuration;
using PosFlow.Api.Filters;
using PosFlow.Api.HealthChecks;
using PosFlow.Api.Middleware;
using PosFlow.Application.Auth;
using PosFlow.Application.Branches;
using PosFlow.Application.Categories;
using PosFlow.Application.Customers;
using PosFlow.Infrastructure.Customers;
using PosFlow.Application.ExchangeRates;
using PosFlow.Infrastructure.ExchangeRates;
using PosFlow.Application.Orders;
using PosFlow.Application.Products;
using PosFlow.Application.Reports;
using PosFlow.Application.Shifts.Validators;
using PosFlow.Application.Users;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Authentication;
using PosFlow.Infrastructure.Branches;
using PosFlow.Infrastructure.Categories;
using PosFlow.Infrastructure.Email;
using PosFlow.Infrastructure.Orders;
using PosFlow.Infrastructure.Persistence;
using PosFlow.Infrastructure.Products;
using PosFlow.Infrastructure.Reports;
using PosFlow.Infrastructure.Users;
using PosFlow.Application.Common;
using PosFlow.Application.Shifts;
using PosFlow.Infrastructure.Shifts;
using Microsoft.OpenApi;
using Prometheus;
using Serilog;
using Serilog.Events;

// Note: deliberately NOT using a static Log.Logger + CreateBootstrapLogger
// two-stage setup here. That pattern is fine for a real process, but
// WebApplicationFactory-based integration tests build this Program's
// host more than once in the same process, and a single static
// Serilog.Log.Logger gets "frozen" on first use - the second host
// build then throws. Configuring Serilog purely through
// Host.UseSerilog(...) below gives each host its own logger instance
// and works identically in production and in tests.
static async Task RunApp(string[] args)
{

var builder = WebApplication.CreateBuilder(args);

// Optional real secrets-manager integration: set KeyVault__Uri (env
// var) to pull secrets from Azure Key Vault instead of/in addition to
// environment variables. Off by default - nothing changes for anyone
// not using Azure. DefaultAzureCredential works with a managed
// identity in Azure, or `az login` locally for testing against a real
// vault. Key Vault secret names use "--" where config uses ":" (e.g.
// a Key Vault secret named "Jwt--Key" maps to Jwt:Key) - Azure Key
// Vault doesn't allow ":" in secret names.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new Azure.Identity.DefaultAzureCredential());
}

// Application Insights, registered only when a connection string is present. Set
// APPLICATIONINSIGHTS_CONNECTION_STRING (or ApplicationInsights:ConnectionString) to enable it.
//
// Gated rather than called unconditionally: AddApplicationInsightsTelemetry() with no connection
// string still installs the whole telemetry pipeline - modules, processors, a background channel -
// which then buffers and drops everything it collects. Pure overhead in every local run and every
// test, for output nobody reads.
//
// This does not replace Prometheus /metrics or Serilog; it is the APM layer those two do not
// cover. Placed below the Key Vault registration on purpose, so a connection string kept in the
// vault is visible here.
var appInsightsConnectionString =
    builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];

if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry(options =>
        options.ConnectionString = appInsightsConnectionString);
}

builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    // Both sinks name {CorrelationId} explicitly. Serilog's default output template renders only
    // timestamp, level and message, so an enriched property is attached to the event and then
    // silently dropped on the way out - the middleware, the enricher and the log all look correct
    // while the id appears nowhere. Verified by making a request with a known id and grepping the
    // log for it, which is the only way this particular failure shows up.
    //
    // The (none) fallback covers log lines written outside a request, where there is no id to
    // attach: startup, shutdown and background work.
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/posflow-.log",
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

const string AuthRateLimitPolicy = "auth";
const string SessionRateLimitPolicy = "auth-session";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Blunts brute-force login/password-reset attempts: 5 attempts
    // per minute, keyed per client IP (not per-account, since the
    // account may not exist yet - this is deliberately IP-based).
    //
    // The ceiling is configurable, and it defaults to the strict value. Only something that sets
    // RateLimiting:AuthPermitLimit explicitly gets anything looser - see .github/workflows/e2e.yml,
    // which is the one place that does.
    //
    // Why it needs to be adjustable at all: every browser test signs in, and they all arrive from
    // one IP, so five is spent about six tests into the E2E suite. The next sign-in gets a 429 and
    // fails looking exactly like a broken login. That happened when the accessibility specs were
    // added and pushed the count past five - an hour to diagnose, and the tempting "fix" is to
    // delete whichever test tipped it over.
    //
    // Why configuration rather than `IsDevelopment()`, which was the first attempt: the
    // integration-test factory runs in Development, so keying off the environment silently raised
    // the ceiling underneath AuthRateLimitTests too. That suite exists to prove login is still
    // throttled, and it went red immediately - correctly. Defaulting to 5 means the tests keep
    // exercising the real limit and only an explicit opt-in relaxes it.
    var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 5);

    options.AddPolicy(AuthRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Refresh and logout sat on the 5/min policy above until a browser test
    // measured what that does to ordinary use: log in, then reload the page
    // four times, and the fourth reload returns 429. restoreSession() treats a
    // failed refresh as "no session" and signs the user out. Reloading a page
    // four times in a minute is not abuse.
    //
    // It is worse than it looks for this product specifically. The limit is
    // keyed per IP, and a shop's terminals share one public address, so several
    // cashiers reloading spend each other's budget and sign each other out.
    //
    // Neither endpoint is the brute-force surface the policy above exists for.
    // Refresh is authenticated by a high-entropy, single-use, rotating token in
    // an HttpOnly cookie -- 5/min is not what makes guessing it infeasible. And
    // throttling logout is its own hazard: a user on a shared terminal who
    // cannot sign out is an exposure, not a protected resource.
    //
    // Login, 2FA verification and the password-reset endpoints keep the strict
    // limit, which is where the comment above actually applies: those take a
    // guessable secret (a password, a six-digit code, a reset token).
    options.AddPolicy(SessionRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                // Far above any human reload rate, and still a ceiling on a
                // runaway client that retries refresh in a loop.
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Global fallback for every other endpoint (checkout included,
    // which previously had no rate limiting at all - see
    // ENTERPRISE-READINESS.md). Generous enough for real POS usage
    // (a busy cashier easily does 1-2 requests/second) while still
    // blunting scripted abuse or a runaway client bug. Per-user where
    // authenticated (so one bad client can't exhaust another user's
    // budget), per-IP otherwise.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext =>
        {
            var key = httpContext.User.Identity?.IsAuthenticated == true
                ? $"user:{httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value}"
                : $"ip:{httpContext.Connection.RemoteIpAddress}";

            return RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
        });
});

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();

    // Adds api/v1/... beside the existing api/... - see VersionedRouteConvention
    options.Conventions.Add(new VersionedRouteConvention());
});

// API versioning is free to add while there are no external clients, and a breaking change
// requiring a migration window once there are.
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);

        // Unversioned routes are treated as v1, so the existing Angular client keeps working
        // untouched.
        options.AssumeDefaultVersionWhenUnspecified = true;

        // Advertises api-supported-versions on responses, so a client can discover what exists
        // without out-of-band documentation.
        options.ReportApiVersions = true;

        // The version is read from the route itself (api/v1/...), which is what
        // VersionedRouteConvention generates. Without this the package falls back to its default
        // QueryStringApiVersionReader: that happens to work, but only because
        // AssumeDefaultVersionWhenUnspecified covers every request lacking ?api-version. It also
        // raises AV0015 and leaves the configuration describing a versioning scheme we do not use.
        options.ApiVersionReader = new Asp.Versioning.UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddValidatorsFromAssemblyContaining<
    OpenShiftRequestValidator>();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "أدخل التوكن بالشكل: Bearer {token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddDbContext<PosFlowDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"));
});

const string CorsPolicyName = "PosFlowCors";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Required for the HttpOnly refresh cookie to work at all. The SPA
                // sends withCredentials, and without this header the browser
                // rejects the whole response before the app ever sees it - the
                // symptom is login simply failing, with no error from the server.
                //
                // Safe here because the origins are explicitly configured;
                // credentials are forbidden with AllowAnyOrigin.
                .AllowCredentials();
        }
    });
});

builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(
        JwtOptions.SectionName));

builder.Services.AddScoped<
    IPasswordHasher<AppUser>,
    PasswordHasher<AppUser>>();

builder.Services.AddScoped<
    IAuthService,
    AuthService>();

// Runs before the JWT key is used for anything. Outside Development this rejects the checked-in
// placeholder secrets, which the length check below cannot catch on its own - the shipped default
// key is 60 characters and passes it happily. See SecretsValidator for why matching is
// pattern-based rather than a list of known values.
SecretsValidator.EnsureProductionSecretsAreConfigured(
    builder.Configuration,
    builder.Environment);

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT settings are missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key) ||
    jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException(
        "JWT key must contain at least 32 characters.");
}

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,

                IssuerSigningKey =
                    new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(
                            jwtOptions.Key)),

                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateLifetime = true,

                ClockSkew = TimeSpan.FromMinutes(1)
            };
    });

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

builder.Services.AddAuthorization(options =>
{
    // One ASP.NET Core policy per entry in the Permissions catalog -
    // see PermissionRequirement/PermissionAuthorizationHandler for how
    // a policy resolves against the user's role.
    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(
            permission,
            policy => policy.Requirements.Add(
                new PermissionRequirement(permission)));
    }
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// Distributed cache. Redis when ConnectionStrings:Redis is set, otherwise an in-memory
// implementation of the same IDistributedCache interface.
//
// The fallback is deliberate rather than lazy. Requiring Redis unconditionally would mean local
// development, CI and the whole test suite all need a Redis server running to do anything - so the
// realistic outcome is that someone disables the cache instead. This way the code path is identical
// everywhere and only the backing store changes.
//
// The fallback is NOT equivalent in a scaled deployment, and that is the point of configuring
// Redis: with the in-memory store, a write on one instance does not invalidate another instance's
// copy, so a category edit can stay invisible on other instances until the entry expires.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");

if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        // Keyspace prefix, so several apps can share one Redis instance without colliding.
        options.InstanceName = "posflow:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddScoped<
    ICurrentUser,
    CurrentUserService>();

builder.Services.AddScoped<
    ICurrentTenantProvider,
    CurrentTenantProvider>();

builder.Services.AddScoped<
    IShiftService,
    ShiftService>();

builder.Services.AddScoped<
    IProductService,
    ProductService>();

builder.Services.AddScoped<
    IOrderService,
    OrderService>();

builder.Services.AddScoped<
    IReceiptPdfService,
    ReceiptPdfService>();

builder.Services.AddScoped<
    IUserService,
    UserService>();

builder.Services.AddScoped<
    ICategoryService,
    CategoryService>();

builder.Services.AddScoped<
    IBranchService,
    BranchService>();

builder.Services.AddScoped<
    IReportService,
    ReportService>();

builder.Services.AddScoped<
    ICustomerService,
    CustomerService>();

builder.Services.AddScoped<
    IExchangeRateService,
    ExchangeRateService>();

builder.Services.Configure<SmtpOptions>(
    builder.Configuration.GetSection(SmtpOptions.SectionName));

// Real SMTP delivery kicks in as soon as Smtp:Host is configured
// (environment variable Smtp__Host, or a secrets manager) - falls
// back to logging the email for local development so forgot-password
// stays usable without any mail infrastructure.
var smtpHost = builder.Configuration["Smtp:Host"];

if (!string.IsNullOrWhiteSpace(smtpHost))
{
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
}

// Email delivery moved off the request thread. Registered as one singleton exposed under two
// types: the hosted service reads the channel, request handlers write to it, and both need the
// same instance -- resolving IBackgroundEmailQueue separately would hand writers a different
// channel from the one being drained, and every queued email would sit unread forever.
builder.Services.AddSingleton<BackgroundEmailQueue>();
builder.Services.AddSingleton<IBackgroundEmailQueue>(sp => sp.GetRequiredService<BackgroundEmailQueue>());
builder.Services.AddHostedService<BackgroundEmailSenderService>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>(
        "database",
        tags: ["ready"]);

var app = builder.Build();

// Auto-migrate is opt-in via config (defaults to true in Development
// through appsettings.Development.json-free convenience, but must be
// set explicitly for any other environment). In real production
// deployments, prefer running migrations as an explicit, reviewed
// pipeline step (see deploy/README.md) and leave this flag off, so a
// second concurrently-starting instance never races a schema change.
var autoMigrate = builder.Configuration.GetValue(
    "App:AutoMigrateOnStartup",
    app.Environment.IsDevelopment());

if (autoMigrate)
{
    using var migrateScope = app.Services.CreateScope();
    var dbContext = migrateScope.ServiceProvider
        .GetRequiredService<PosFlowDbContext>();

    await DatabaseSeeder.MigrateAsync(dbContext);
}

if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var dbContext = seedScope.ServiceProvider
        .GetRequiredService<PosFlowDbContext>();
    var passwordHasher = seedScope.ServiceProvider
        .GetRequiredService<IPasswordHasher<AppUser>>();

    await DatabaseSeeder.SeedDemoDataAsync(dbContext, passwordHasher);
}
else if (builder.Configuration.GetValue("App:BootstrapAdminIfEmpty", false))
{
    using var bootstrapScope = app.Services.CreateScope();
    var dbContext = bootstrapScope.ServiceProvider
        .GetRequiredService<PosFlowDbContext>();
    var passwordHasher = bootstrapScope.ServiceProvider
        .GetRequiredService<IPasswordHasher<AppUser>>();
    var logger = bootstrapScope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();

    await DatabaseSeeder.BootstrapProductionAdminIfEmptyAsync(
        dbContext, passwordHasher, logger);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Ahead of request logging so the correlation id is already on the Serilog context when the
// request-completed line is written - otherwise the one line most likely to be searched for is
// the only one without an id on it.
app.UseMiddleware<PosFlow.Api.Middleware.CorrelationIdMiddleware>();

// Request logging goes first so it observes the FINAL response status,
// including ones rewritten by the exception handler below (otherwise
// it logs the pre-handler status, which is misleading for errors).
app.UseSerilogRequestLogging();

app.UseExceptionHandler();

// Gives a ProblemDetails body to responses that would otherwise be a bare status code - the 401,
// 403 and 404 the framework generates itself. Without this a caller receives an empty body and has
// only the number to work from, unable to distinguish an expired token from a wrong route.
//
// Validation errors (400) already return full ProblemDetails from ASP.NET Core, and unhandled
// exceptions are covered by the handler above; this closes the remaining gap between them.
app.UseStatusCodePages();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

    if (!app.Environment.IsDevelopment())
    {
        // This is a JSON API with no HTML views of its own outside
        // Swagger, which is dev-only - so a locked-down CSP costs
        // nothing in every other environment and blocks this response
        // from ever being embedded/executed as active content if it
        // somehow ended up rendered somewhere. Left off in
        // Development because Swagger UI needs its own scripts/styles.
        context.Response.Headers.Append(
            "Content-Security-Policy",
            "default-src 'none'; frame-ancestors 'none'");

        // 1 year, include subdomains - standard HSTS preload
        // candidate values. Only sent over HTTPS responses in
        // practice, but harmless to always set.
        context.Response.Headers.Append(
            "Strict-Transport-Security",
            "max-age=31536000; includeSubDomains");
    }

    await next();
});

app.UseHttpsRedirection();

// HTTP request metrics (count/duration/in-flight, labelled by route,
// method, status code) for a self-hosted Prometheus/Grafana - see
// /metrics below. No external monitoring account or SaaS dependency;
// whoever runs this instance points their own Prometheus at it.
app.UseHttpMetrics();

app.UseCors(CorsPolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// /health kept for backwards compatibility with anything already
// pointed at it (see HANDOVER.md §6). /health/live is a pure
// process-is-up check for a container orchestrator's liveness probe
// (no dependencies - must never fail just because the DB is slow, or
// the orchestrator will kill and restart a perfectly fine container).
// /health/ready additionally checks the DB, for a readiness probe
// (safe to take out of a load balancer's rotation, not to kill).
app.MapHealthChecks("/health");

app.MapHealthChecks("/health/live", new()
{
    Predicate = check => false
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

// Prometheus text-format scrape endpoint. Deliberately unauthenticated
// (Prometheus itself has no bearer-token login flow) - like /health,
// protect it at the network layer in production (internal-only
// ingress rule, or a reverse-proxy rule restricting /metrics to the
// monitoring subnet) rather than app-level auth.
app.MapMetrics("/metrics");

app.Run();

}

await RunApp(args);

public partial class Program;