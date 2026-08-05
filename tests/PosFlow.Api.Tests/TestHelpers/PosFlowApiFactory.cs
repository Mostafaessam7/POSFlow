using Microsoft.AspNetCore.Hosting;
using System.Linq;
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
            // Removing just DbContextOptions<T> isn't enough in modern
            // EF Core - AddDbContext/UseSqlServer also register
            // provider-specific internal services (e.g.
            // IDbContextOptionsConfiguration<T>) that conflict with a
            // second UseInMemoryDatabase() registration ("Services for
            // database providers X, Y have been registered"). Strip
            // every descriptor tied to PosFlowDbContext before
            // re-registering it against InMemory.
            services.RemoveAll<DbContextOptions<PosFlowDbContext>>();
            services.RemoveAll<PosFlowDbContext>();

            var efDescriptors = services
                .Where(d =>
                    d.ServiceType.FullName?.Contains(
                        nameof(PosFlowDbContext)) == true)
                .ToList();

            foreach (var descriptor in efDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<PosFlowDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });
        });
    }
}
