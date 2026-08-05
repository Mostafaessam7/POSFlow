using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Api.Tests.TestHelpers;

/// <summary>
/// Boots the real API (real Program.cs, real middleware pipeline,
/// real controllers/services/validators) in-process, with the only
/// swap being SQL Server -> a fresh isolated EF Core InMemory
/// database per factory instance. This exercises auth, the
/// ValidationFilter, the GlobalExceptionHandler, and routing exactly
/// as they run in production.
/// </summary>
public sealed class PosFlowApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PosFlowDbContext>>();

            services.AddDbContext<PosFlowDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }
}
