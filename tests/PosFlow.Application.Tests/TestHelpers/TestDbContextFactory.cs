using Microsoft.EntityFrameworkCore;
using PosFlow.Application.Common;
using PosFlow.Infrastructure.Persistence;

namespace PosFlow.Application.Tests.TestHelpers;

/// <summary>Fixed tenant id for tests that want the query filter enforced.</summary>
public sealed class FakeCurrentTenantProvider(Guid? tenantId = null)
    : ICurrentTenantProvider
{
    public Guid? TenantId => tenantId;
}

public static class TestDbContextFactory
{
    /// <summary>
    /// Creates a fresh, isolated in-memory database by default. Pass an
    /// explicit databaseName when a test needs two separate
    /// PosFlowDbContext instances (e.g. simulating two users) to see
    /// the same underlying data - useful for concurrency tests.
    ///
    /// By default the tenant query filter is bypassed (tenantId: null)
    /// so existing tests that filter manually keep working unchanged.
    /// Pass a tenantId to exercise the global query filter itself
    /// (see TenantIsolationTests).
    /// </summary>
    public static PosFlowDbContext Create(
        string? databaseName = null,
        Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<PosFlowDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new PosFlowDbContext(
            options,
            new FakeCurrentTenantProvider(tenantId));
    }
}
