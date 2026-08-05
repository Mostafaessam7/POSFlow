# ADR 0002: Migrations are an explicit deploy step, not automatic on boot

**Status:** Accepted — 5 August 2026

## Context

The app previously called `Database.MigrateAsync()` unconditionally on
every startup, in every environment. That's convenient for a single
local dev instance, but risky for a real deployment: if more than one
instance starts concurrently (a rolling deploy, an autoscale event), two
processes can race the same schema migration. It also means a schema
change ships with no review step distinct from the code deploy itself.

## Decision

`App:AutoMigrateOnStartup` (config) controls this, defaulting to `true`
only in `Development` (via a code default, not a committed config
value) and `false` everywhere else. Production deploys must run
migrations as an explicit step before rolling out the new app version -
see `deploy/README.md` §2.

`docker-compose.yml` (local/dev only) sets it to `true` explicitly,
which is fine there since it's a single instance.

## Consequences

- Production deploys have one more manual (or CD-scripted) step.
- No more risk of two instances racing a schema migration.
- `dotnet ef migrations script --idempotent` becomes the way to review
  what a deploy will actually run against a real database before it
  happens.
