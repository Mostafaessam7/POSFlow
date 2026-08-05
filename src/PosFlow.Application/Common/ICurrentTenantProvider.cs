namespace PosFlow.Application.Common;

/// <summary>
/// Supplies the tenant id for the current EF Core global query filter.
/// Unlike <see cref="ICurrentUser"/>, this must never throw - it is
/// evaluated on every query, including ones that run with no HTTP
/// request in scope (startup seeding, migrations, background jobs).
/// When there is no authenticated request, <see cref="TenantId"/> is
/// null and the query filter is bypassed (system/trusted context
/// only - never reachable from an external HTTP call, since every
/// controller requires [Authorize] and a valid tenant_id claim).
/// </summary>
public interface ICurrentTenantProvider
{
    Guid? TenantId { get; }
}
