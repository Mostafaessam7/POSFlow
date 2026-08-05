using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using Xunit;

namespace PosFlow.Application.Tests.Common;

public sealed class AuditLogTests
{
    [Fact]
    public async Task CreatingAProduct_WritesAnAuditLogRow()
    {
        var tenantId = Guid.NewGuid();

        await using var dbContext = TestDbContextFactory.Create(
            tenantId: tenantId);

        dbContext.Products.Add(new Product
        {
            TenantId = tenantId,
            NameAr = "قهوة",
            Price = 40m,
            IsActive = true
        });

        await dbContext.SaveChangesAsync();

        var auditRow = Assert.Single(dbContext.AuditLogs.ToList());
        Assert.Equal(nameof(Product), auditRow.EntityName);
        Assert.Equal(AuditAction.Created, auditRow.Action);
    }

    [Fact]
    public async Task UpdatingAProduct_WritesAnAuditLogRowWithOldAndNewPrice()
    {
        var tenantId = Guid.NewGuid();
        var databaseName = Guid.NewGuid().ToString();

        Guid productId;

        await using (var seedDb = TestDbContextFactory.Create(
            databaseName, tenantId))
        {
            var product = new Product
            {
                TenantId = tenantId,
                NameAr = "قهوة",
                Price = 40m,
                IsActive = true
            };

            seedDb.Products.Add(product);
            await seedDb.SaveChangesAsync();
            productId = product.Id;
        }

        await using var dbContext = TestDbContextFactory.Create(
            databaseName, tenantId);

        var tracked = dbContext.Products.Single(x => x.Id == productId);
        tracked.Price = 55m;

        await dbContext.SaveChangesAsync();

        var updateAudit = dbContext.AuditLogs
            .Single(x => x.Action == AuditAction.Updated);

        Assert.Contains("Price", updateAudit.ChangesJson);
        Assert.Contains("55", updateAudit.ChangesJson);
    }
}
