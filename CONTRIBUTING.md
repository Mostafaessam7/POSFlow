# Contributing to PosFlow

Repo: https://github.com/Mostafaessam7/POSFlow

## Getting set up

Backend:
```bash
dotnet restore PosFlow.slnx
dotnet build PosFlow.slnx
dotnet test PosFlow.slnx
```

Frontend:
```bash
cd posflow-web
npm ci
npm run build
npm test -- --watch=false
```

Both must be clean (0 errors, all tests passing) before you open a PR -
CI (`.github/workflows/ci.yml`) enforces this, including a NuGet/npm
vulnerability scan and a Docker image build.

## Branching & commits

- Never commit directly to `main` - branch first.
- Keep commits focused; write the *why*, not just the *what*, in the
  body when it isn't obvious from the diff.
- Don't force-push shared branches.

## Code style

- Backend: `.NET` nullable reference types are on - don't suppress
  warnings, fix them. FluentValidation for all request DTOs (wired up
  globally via `ValidationFilter` - a validator with no explicit
  registration in a controller just needs to exist in the Application
  assembly).
- Frontend: standalone Angular components, no NgModules. This app is
  **zoneless** (no `zone.js` dependency) - don't add code or specs
  that assume zone.js is present (no `fakeAsync`/`tick`,
  `jasmine.clock()`, etc. - see git history around August 2026 for
  examples of migrating away from these to Vitest's `vi.useFakeTimers()`).

## Multi-tenancy - read this before touching any query

Every tenant-scoped entity is protected by **two** independent layers
(see the class doc on `PosFlowDbContext`):

1. Manual `.Where(x => x.TenantId == _currentUser.TenantId)` in every
   service - keep doing this, it's still required for correctness with
   composite unique indexes.
2. An EF Core global query filter, driven by `ICurrentTenantProvider`,
   that's a safety net for (1) being forgotten.

If you add a new entity with a `TenantId`, add its `HasQueryFilter(...)`
in `PosFlowDbContext.OnModelCreating` too, and add a case to
`TenantIsolationTests` proving cross-tenant rows stay hidden even
without the manual filter. Losing tenant isolation is the single worst
class of bug this app can ship.

## Audit trail

`Order`, `Product`, `AppUser`, `Branch`, and `Shift` changes are
automatically captured to `AuditLog` (see `PosFlowDbContext.AuditedEntityTypes`).
Add a new entity type to that set if it holds anything a shop owner or
auditor would reasonably want a "who changed this, and when" trail for.

## Database changes

Add a migration for any model change:
```bash
dotnet ef migrations add YourMigrationName \
  --project src/PosFlow.Infrastructure \
  --startup-project src/PosFlow.Api \
  --output-dir Persistence/Migrations
```
Never hand-edit a migration that's already been applied anywhere real -
add a new one instead.

## Secrets

Never commit real connection strings, JWT keys, or SMTP credentials.
`appsettings.json` ships secret-free by design - see `.env.example` for
the full list of what production needs and where it comes from.
