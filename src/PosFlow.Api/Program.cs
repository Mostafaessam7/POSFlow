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

builder.Services.AddScoped<
    IEmailSender,
    LoggingEmailSender>();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

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

    await next();
});

app.UseHttpsRedirection();

app.UseCors(CorsPolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

}

await RunApp(args);

public partial class Program;