using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PosFlow.Api.Filters;
using PosFlow.Api.HealthChecks;
using PosFlow.Api.Middleware;
using PosFlow.Application.Auth;
using PosFlow.Application.Branches;
using PosFlow.Application.Categories;
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

builder.Host.UseSerilog((context, services, loggerConfig) => loggerConfig
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/posflow-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

const string AuthRateLimitPolicy = "auth";

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Blunts brute-force login/password-reset attempts: 5 attempts
    // per minute, keyed per client IP (not per-account, since the
    // account may not exist yet - this is deliberately IP-based).
    options.AddPolicy(AuthRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
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
                .AllowAnyMethod();
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

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

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

// Request logging goes first so it observes the FINAL response status,
// including ones rewritten by the exception handler below (otherwise
// it logs the pre-handler status, which is misleading for errors).
app.UseSerilogRequestLogging();

app.UseExceptionHandler();

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

app.Run();

}

await RunApp(args);

public partial class Program;