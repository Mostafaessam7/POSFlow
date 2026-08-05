# Deploying PosFlow

This is a deployment runbook, not a fully automated pipeline yet (there
is no CD job in `.github/workflows/ci.yml` - see the note at the bottom
of that file). Follow these steps manually (or wire them into a real
pipeline once you have a hosting target) for every release.

## 1. Secrets

Never put real values in `appsettings.json` (it's meant to ship secret-free -
see `.env.example` for the full list). Load these from your platform's
secrets manager / environment variables instead:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key` (32+ random characters - `openssl rand -base64 48`)
- `Cors__AllowedOrigins__0` (your real frontend origin)
- `App__FrontendBaseUrl`
- `Smtp__*` (real SMTP provider credentials)

## 2. Database migrations - explicit step, not automatic

`App__AutoMigrateOnStartup` defaults to `false` outside Development on
purpose (see `ENTERPRISE-READINESS.md` §1 - auto-migrating on every
boot risks two instances racing a schema change, and skips review).

Run migrations yourself, before deploying the new app version:

```bash
dotnet ef database update \
  --project src/PosFlow.Infrastructure \
  --startup-project src/PosFlow.Api \
  --connection "<your real production connection string>"
```

Review the SQL a migration will run before applying it against a real
database you care about:

```bash
dotnet ef migrations script \
  --project src/PosFlow.Infrastructure \
  --startup-project src/PosFlow.Api \
  --idempotent
```

## 3. First-ever deploy to a brand new database

There's no admin user yet. Either:

- Set `App__BootstrapAdminIfEmpty=true` for the FIRST deploy only, then
  back to `false`. It creates one tenant/branch/admin with a randomly
  generated password logged once at Warning level - capture it from
  your log aggregator immediately and change it via the app, then turn
  the flag back off.
- Or seed the first tenant/admin directly via SQL if you'd rather not
  rely on the logged-password flow.

## 4. Build & run

```bash
docker build -f src/PosFlow.Api/Dockerfile -t posflow-api:<version> .
docker build -t posflow-web:<version> ./posflow-web
```

`docker-compose.yml` at the repo root is for local/dev only (it runs
its own SQL Server container and auto-migrates on boot) - point your
real deploy at your actual database and secrets manager instead.

## 5. Health checks

Point your platform's probes at:

- `GET /health/live` - liveness (process is up, no dependencies checked)
- `GET /health/ready` - readiness (checks DB connectivity)

## 6. Backups

- **Managed database (Azure SQL, RDS SQL Server, ...):** use the
  platform's own automated backup / point-in-time recovery - it's more
  robust than anything this repo could script, and usually already on
  by default. Just confirm the retention window meets your needs.
- **Self-hosted SQL Server (a VM, on-prem, the docker-compose setup):**
  nothing does this for you automatically. Use `deploy/backup-database.ps1`,
  scheduled daily via Windows Task Scheduler or cron (`pwsh
  deploy/backup-database.ps1 -ServerInstance ... -BackupDirectory ...
  -SaPassword ...`). Defaults to 30-day retention.
- Either way: **test a restore at least once** before you need it for
  real - an untested backup is not a backup. The script's doc comment
  includes a `RESTORE DATABASE` example.

## 7. Rollback

Because migrations are a separate, explicit step (§2), rolling back the
app container to a previous image is safe as long as you haven't also
rolled the database forward past what that image expects. If a bad
migration needs undoing:

```bash
dotnet ef database update <PreviousMigrationName> \
  --project src/PosFlow.Infrastructure \
  --startup-project src/PosFlow.Api
```
