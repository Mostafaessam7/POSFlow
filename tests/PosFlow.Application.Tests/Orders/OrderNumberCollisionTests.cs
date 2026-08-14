using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PosFlow.Application.Orders;
using PosFlow.Application.Tests.TestHelpers;
using PosFlow.Domain.Entities;
using PosFlow.Infrastructure.Orders;
using PosFlow.Infrastructure.Persistence;
using Xunit;

namespace PosFlow.Application.Tests.Orders;

/// <summary>
/// Exercises OrderService's order-number retry loop against a REAL
/// unique constraint. The EF Core InMemory provider (used by the rest
/// of OrderServiceTests) never raises DbUpdateException for a
/// duplicate value on a unique index - see OrderService.CheckoutAsync's
/// own comments - so this path was previously untested (called out
/// explicitly in HANDOVER.md/ENTERPRISE-READINESS.md as a known gap).
/// SQLite is used here instead purely because it's in-process and
/// enforces unique indexes the same way SQL Server does, unlike
/// InMemory - it is not a stand-in for "tested against SQL Server".
/// </summary>
public sealed class OrderNumberCollisionTests
{
    [Fact]
    public async Task CheckoutAsync_ConcurrentCheckouts_RetryProducesUniqueOrderNumbers()
    {
        var dbPath = Path.Combine(
            Path.GetTempPath(),
            $"posflow-ordernumber-{Guid.NewGuid():N}.db");

        var connectionString = $"Data Source={dbPath}";

        try
        {
            const int cashierCount = 5;

            var tenantId = Guid.NewGuid();
            var branchId = Guid.NewGuid();
            var userIds = Enumerable.Range(0, cashierCount)
                .Select(_ => Guid.NewGuid())
                .ToArray();

            Guid productId;

            await using (var setupContext = CreateSqliteContext(connectionString, tenantId))
            {
                await setupContext.Database.EnsureCreatedAsync();

                setupContext.Tenants.Add(new Tenant
                {
                    Id = tenantId,
                    Name = "متجر تجريبي"
                });

                setupContext.Branches.Add(new Branch
                {
                    Id = branchId,
                    TenantId = tenantId,
                    Name = "الفرع الرئيسي",
                    Code = "MAIN"
                });

                var product = new Product
                {
                    TenantId = tenantId,
                    NameAr = "منتج تجريبي",
                    Price = 10m,
                    TrackStock = false,
                    IsActive = true
                };

                setupContext.Products.Add(product);

                foreach (var userId in userIds)
                {
                    setupContext.Shifts.Add(new Shift
                    {
                        TenantId = tenantId,
                        BranchId = branchId,
                        UserId = userId,
                        OpeningCash = 0,
                        Status = ShiftStatus.Open,
                        OpenedAtUtc = DateTime.UtcNow
                    });
                }

                await setupContext.SaveChangesAsync();

                productId = product.Id;
            }

            // Several "cashiers" checking out at the same moment, each
            // with their own open shift/DbContext (a DbContext isn't
            // thread-safe to share). All of them read today's order
            // count before any of them commit, so - like real
            // concurrent requests - several are likely to compute the
            // same next order number; SQLite's writer lock serializes
            // the SaveChanges calls, so whoever commits after the
            // first with the same number hits the unique index and
            // must retry (this is what was previously untested).
            var request = new CreateOrderRequest(
                Lines: [new OrderLineRequest(productId, 1, 0)],
                Payments: [new PaymentRequest(PaymentMethod.Cash, 10, null)]);

            var checkoutTasks = userIds
                .Select(userId =>
                {
                    var service = new OrderService(
                        CreateSqliteContext(connectionString, tenantId),
                        new FakeCurrentUser { TenantId = tenantId, BranchId = branchId, UserId = userId });

                    return service.CheckoutAsync(request);
                })
                .ToArray();

            var results = await Task.WhenAll(checkoutTasks);

            Assert.All(results, r => Assert.Equal("Completed", r.Status));

            Assert.Equal(
                cashierCount,
                results.Select(r => r.OrderNumber).Distinct().Count());
        }
        finally
        {
            SqliteConnection.ClearAllPools();

            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                var path = dbPath + suffix;

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    private static PosFlowDbContext CreateSqliteContext(
        string connectionString,
        Guid tenantId)
    {
        // RowVersion is mapped via .IsRowVersion(), which on SQL Server
        // means "the server auto-assigns this on INSERT, don't send a
        // value" - EF Core honours that by omitting the column from
        // the INSERT entirely, regardless of provider. SQLite has no
        // equivalent auto-assignment, so the column would come back
        // NULL and violate the NOT NULL constraint. Swapping it to
        // ValueGenerated.Never for this SQLite-only test context makes
        // EF send the entity's actual (default-initialized, non-null)
        // RowVersion byte[] instead - harmless here since this test
        // doesn't exercise optimistic-concurrency conflicts, only the
        // order-number unique-index race.
        var options = new DbContextOptionsBuilder<PosFlowDbContext>()
            .UseSqlite(connectionString)
            .ReplaceService<IModelCustomizer, RowVersionNeverGeneratedModelCustomizer>()
            .Options;

        return new PosFlowDbContext(
            options,
            new FakeCurrentTenantProvider(tenantId));
    }

    private sealed class RowVersionNeverGeneratedModelCustomizer : ModelCustomizer
    {
        public RowVersionNeverGeneratedModelCustomizer(ModelCustomizerDependencies dependencies)
            : base(dependencies)
        {
        }

        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var rowVersion = entityType.FindProperty("RowVersion");

                if (rowVersion is not null)
                {
                    rowVersion.ValueGenerated = ValueGenerated.Never;
                }
            }
        }
    }
}
