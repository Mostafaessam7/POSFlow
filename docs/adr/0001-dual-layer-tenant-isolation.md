# ADR 0001: Dual-layer tenant isolation

**Status:** Accepted — 5 August 2026

## Context

PosFlow is multi-tenant: every shop's data lives in the same database,
distinguished by `TenantId`. Before this decision, isolation was
enforced only by each service manually filtering
`.Where(x => x.TenantId == _currentUser.TenantId)`. That's correct when
remembered, but nothing stopped a future endpoint from forgetting it -
and forgetting it means one tenant's sales, products, or staff data
leaking to another. That's the worst class of bug a multi-tenant POS
can ship.

## Decision

Keep the manual per-service filter (still needed for correctness with
composite unique indexes like `(TenantId, Code)`), **and** add an EF
Core global query filter on every tenant-scoped entity in
`PosFlowDbContext`, driven by `ICurrentTenantProvider`. The provider
never throws (unlike `ICurrentUser`) - it returns `null` when there's
no authenticated HTTP request, which only happens in trusted
system/startup contexts (seeding, migrations), never from an external
call, since every controller requires `[Authorize]` and a valid
`tenant_id` claim.

`TenantIsolationTests` (in `PosFlow.Application.Tests`) proves the
filter alone hides cross-tenant rows even when a query has no manual
`TenantId` filter at all - simulating a future service that forgot it.

## Consequences

- A forgotten manual filter becomes a correctness/performance nit, not
  a data leak.
- Every new tenant-scoped entity must add its own
  `HasQueryFilter(...)` - documented in `CONTRIBUTING.md`.
- System/seeding code that legitimately needs cross-tenant access
  works because `ICurrentTenantProvider.TenantId` is null outside an
  HTTP request - this must never be papered over by, say, injecting a
  fake "always authenticated" HttpContext into a background job.
