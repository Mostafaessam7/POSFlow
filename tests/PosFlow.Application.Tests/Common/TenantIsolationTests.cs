using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using Xunit;

namespace PosFlow.Application.Tests.Common;

/// <summary>
/// Guards the second, independent layer of tenant isolation: the EF Core
/// global query filter on PosFlowDbContext (see its class doc). These
/// tests deliberately query the DbContext directly, with NO manual
/// TenantId filter, to prove that a service which forgot to filter
/// would still be safe.
/// </summary>
public sealed class TenantIsolationTests
{
    [Fact]
    public async Task Products_QueriedWithoutManualFilter_OnlyReturnsCurrentTenantRows()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedDb = Guid.NewGuid().ToString();

        await using (var seedDb = TestDbContextFactory.Create(sharedDb))
        {
            seedDb.Products.Add(new Product
            {
                TenantId = tenantA,
                NameAr = "منتج تينانت أ",
                Price = 10m,
                IsActive = true
            });

            seedDb.Products.Add(new Product
            {
                TenantId = tenantB,
                NameAr = "منتج تينانت ب",
                Price = 20m,
                IsActive = true
            });

            await seedDb.SaveChangesAsync();
        }

        await using var dbAsTenantA = TestDbContextFactory.Create(
            sharedDb, tenantId: tenantA);

        // No .Where(TenantId == ...) here on purpose - simulates a
        // service that forgot the manual filter.
        var visibleProducts = dbAsTenantA.Products.ToList();

        Assert.Single(visibleProducts);
        Assert.Equal(tenantA, visibleProducts[0].TenantId);
    }

    [Fact]
    public async Task WithNoTenantInContext_FilterIsBypassed_ForSystemUseOnly()
    {
        var tenantA = Guid.NewGuid();
        var sharedDb = Guid.NewGuid().ToString();

        await using (var seedDb = TestDbContextFactory.Create(sharedDb))
        {
            seedDb.Products.Add(new Product
            {
                TenantId = tenantA,
                NameAr = "منتج",
                Price = 10m,
                IsActive = true
            });

            await seedDb.SaveChangesAsync();
        }

        // tenantId: null simulates a system/background context (e.g.
        // seeding) - the filter must not hide data from it.
        await using var systemDb = TestDbContextFactory.Create(sharedDb);

        Assert.Single(systemDb.Products.ToList());
    }
}
