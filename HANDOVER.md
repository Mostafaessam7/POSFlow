# PosFlow — Handover Document

**Last updated:** 27 August 2026
**Prepared by:** Claude (Anthropic), across an extended pairing session with the project owner.
**GitHub:** https://github.com/Mostafaessam7/POSFlow

This document is a historical record of what was built and why. **It is not the current status doc** — for the up-to-date picture of what's actually done vs. still missing, read [`ENTERPRISE-READINESS.md`](ENTERPRISE-READINESS.md) first, then come back here for the "why" behind the build. The project has a real git history now (`git log` on `main`, ~28 commits from 5 August to 26 August 2026) — the "delivered as numbered zip files" workflow described in §2 below was how the *original* prototype was built, before it became a normal git repo; it is kept here only as history.

---

## 1. What PosFlow is

A multi-tenant, branch-aware Point of Sale system.

- **Backend:** .NET 10, Clean Architecture (`Domain` → `Application` → `Infrastructure` → `Api`), EF Core, SQL Server.
- **Frontend:** Angular 22, standalone components, RTL Arabic UI.
- **Auth:** JWT access tokens + rotating refresh tokens, role-based (`Admin` / `Manager` / `Cashier`).

---

## 2. How this session's work was delivered

Everything was delivered as a sequence of numbered zip files, each with its own `README.txt` explaining exactly where its contents go and what changed. **They must be applied in order**, since later batches build on earlier ones (some batches change constructor signatures, DTOs, or database columns that earlier batches introduced).

| # | Zip file | What it added |
|---|----------|----------------|
| 1 | `posflow-orders-products-validation.zip` | Products & Orders/Payments APIs, FluentValidation wired up globally |
| 2 | `posflow-frontend-pos-products.zip` | POS checkout screen, product list screen |
| 3 | `posflow-history-split-payment.zip` | Shift history screen, split/multi-payment checkout |
| 4 | `posflow-branch-shift-history.zip` | Branch-wide shift history for Admin/Manager |
| 5 | `posflow-critical-backend-improvements.zip` | Global exception handling, CORS, Users API, refresh tokens |
| 6 | `posflow-pagination-stock-void-categories.zip` | Pagination, stock tracking, order void/refund, product categories, RowVersion concurrency, branch management API |
| 7 | `posflow-users-branches-admin-ui.zip` | Admin UI for Users and Branches management |
| 8 | `posflow-dashboard-and-design-refresh.zip` | Sales dashboard, full visual design system overhaul |
| 9 | `posflow-test-suite.zip` | Backend unit tests (xUnit), frontend unit test fix + new test |
| 10 | `posflow-integration-tests.zip` | Backend integration tests (WebApplicationFactory), guard tests |
| 11 | `posflow-remaining-integration-tests.zip` | Integration tests for Categories/Branches/Reports/Shifts branch-history |
| 12 | `posflow-forgot-password-ci-health.zip` | Health check endpoint, GitHub Actions CI, forgot/reset password flow |

**If any of these zips have been lost**, they'll need to be regenerated — this document alone isn't sufficient to reconstruct the code, only to understand what exists and why. In practice this no longer matters: the code lives in git now, so the source of truth is `git log`/`git show`, not the zip archive.

---

## 3. Current feature set (verified against the actual code, 27 August 2026)

### Backend APIs (all under `/api/`)
| Area | Endpoints | Notes |
|---|---|---|
| Auth | `login`, `login/verify-2fa`, `2fa/setup`, `2fa/enable`, `2fa/disable`, `refresh`, `logout`, `forgot-password`, `reset-password`, `me` | Rotating refresh tokens; optional TOTP 2FA (RFC 6238); account lockout after 5 failed attempts (15 min) |
| Products | full CRUD + pagination + category filter + `by-barcode/{barcode}` + `{id}/stock-movements` | RowVersion optimistic concurrency enforced on update; stock changes write an append-only `StockMovement` row |
| Categories | full CRUD | Delete blocked if products still reference it; list is `IMemoryCache`d |
| Customers | full CRUD | Optional link from an order; simple loyalty points (1 point per currency unit) |
| Exchange rates | full CRUD + `/convert` | Manual, admin-maintained per-tenant rates — **display-only conversion, no external FX API** |
| Orders | `checkout`, `by-shift/{id}`, `{id}` (get), `{id}/void`, `{id}/receipt-pdf` | Stock-aware, split payments, tenant tax rate applied, void restores stock; PDF receipt via QuestPDF |
| Shifts | `open`, `{id}/close`, `current`, `history`, `branch-history` | `branch-history` is Admin/Manager only |
| Users | full CRUD + `reset-password` | Admin only |
| Branches | full CRUD | Admin only |
| Reports | `daily-summary` | Admin/Manager only; today's sales, cash/card split, top products |
| — | `/health`, `/health/live`, `/health/ready`, `/metrics` | `/metrics` is a Prometheus scrape endpoint (prometheus-net), unauthenticated by design — protect it at the network layer |

Authorization is policy-based (a `Permissions` catalog + `PermissionAuthorizationHandler`), not raw `[Authorize(Roles=...)]` — but there are still only 3 roles (`Admin`/`Manager`/`Cashier`; see `Roles.cs`), each mapped to a fixed permission set. There is no per-user custom permission assignment yet.

### Frontend screens
Login, Forgot/Reset Password, Open Shift, POS Checkout (with barcode lookup and PDF receipt download), Product List, Shift History, Sales Dashboard, Users admin, Branches admin. The whole app supports an Arabic/English language toggle and a light/dark theme toggle (`core/i18n/`, `core/theme/`).

### Cross-cutting
- Global exception handler (consistent error JSON shape, no more per-controller try/catch), with translated (Arabic/English) error messages
- FluentValidation on every request DTO, enforced by a global action filter
- CORS (configurable allow-list, fails closed if unset)
- Pagination on every list endpoint (`page`/`pageSize` query params)
- Global + auth-specific rate limiting (120 req/min per user/IP; 5/min on auth endpoints)
- Security headers: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, CSP and HSTS (outside Development)
- Serilog structured logging (console + rolling daily JSON files) and Prometheus HTTP metrics
- Automatic `AuditLog` entries for Order/Product/AppUser/Branch/Shift changes
- A cohesive visual design system (CSS custom properties, Tajawal + JetBrains Mono type, warm "receipt paper" palette, dark-mode variants)

### Tests (verified by actually running them, 29 August 2026)
- `tests/PosFlow.Application.Tests` — **41 tests**, unit tests for Shift/Order/Product service logic, validators, tenant isolation, and a SQLite-backed test for the order-number unique-index retry race (EF Core InMemory doesn't enforce unique indexes, so that path needed a real relational provider)
- `tests/PosFlow.Api.Tests` — **62 tests**, integration tests hitting the real HTTP pipeline for every controller (WebApplicationFactory), including cross-tenant HTTP-level security tests, the HttpOnly-cookie auth transport, and the auth rate-limit policy split
- Frontend (`ng test`, Vitest) — **40 tests**: guards, checkout cart/payment-math logic
- `posflow-web/e2e/` — **3 Playwright spec files, 4 tests** (login ×2, core POS sale flow, confirm-dialog keyboard behaviour) against a real backend + SQL Server; run in CI (`.github/workflows/e2e.yml`) against a real `mssql` service container
  - The dialog spec has to be E2E rather than a unit test: jsdom performs no layout, so every element reports zero geometry, the CDK's focus-trap logic finds nothing tabbable, and focus assertions fail for reasons that exist only in jsdom
- `tests/load/posflow-load-test.js` — a k6 load-test script (catalog browsing + a single-VU checkout scenario); not run as part of CI, meant to be run manually against a staging-like environment
- CI (`.github/workflows/ci.yml`) runs backend build/test, frontend build/test, a NuGet vulnerability gate (fails on High/Critical), `npm audit --audit-level=high`, and a Docker image build check on every push/PR to `main`; on push to `main` it also publishes both Docker images to GHCR (no deploy-to-a-server step yet)

**Resolved 28 Aug 2026:** the high-severity advisory for the transitive package `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (`GHSA-2m69-gcr7-jv3q`, pulled in by the test project's SQLite provider) is closed — pinned to 2.1.13 as a direct `PackageReference` in `PosFlow.Application.Tests.csproj`. `dotnet list package --vulnerable --include-transitive` now reports no vulnerable packages in any project.

The CI step that reports this was also decorative until the same date: `dotnet list package --vulnerable` exits 0 even when it finds something, so the step could never fail the build. It now greps its own output and fails on High/Critical, mirroring the frontend's existing `npm audit --audit-level=high` gate. The gate was verified in both directions against real command output (clean → passes, the 2.1.11 finding → fails).

---

## 4. Setup checklist (verified working as of 27 August 2026)

Unlike earlier versions of this document, this has actually been built and tested directly: `dotnet build`, `dotnet test` (103 tests), and `npm test` (40 tests) were all run against the real code and passed. Steps to run it yourself:

1. `dotnet restore PosFlow.slnx && dotnet build PosFlow.slnx`
2. `dotnet test PosFlow.slnx` — should be green (103 tests: 62 API + 41 Application).
3. `dotnet run --project src/PosFlow.Api/PosFlow.Api.csproj` — serves `http://localhost:5000`. Development environment auto-migrates and seeds demo data by default.
4. `cd posflow-web && npm install && npm test -- --watch=false` — should be green (40 tests).
5. `npm start`, then open `http://localhost:4200`. The proxy is wired in `angular.json`; do not pass `--proxy-config` by hand.
6. Optional: `docker compose up` runs API + Angular (behind nginx) + SQL Server together for local use.
7. Optional: E2E — see `posflow-web/e2e/README.md` (needs a real SQL Server reachable, not just unit-test mocks).
8. Walk through one full manual flow yourself: login → open shift → sell something (try the barcode field and the PDF receipt download) → close shift → check the dashboard.

---

## 5. Known limitations (honest list, as of 27 August 2026)

Most of the gaps identified in earlier sessions have since been closed. This list reflects what is **still actually missing**, verified against the current code — see [`ENTERPRISE-READINESS.md`](ENTERPRISE-READINESS.md) for the full, categorized picture.

- **Email works but needs real credentials.** `SmtpEmailSender` exists and is used automatically once `Smtp:Host` is configured; without it, `LoggingEmailSender` just logs the reset link. No assistant can supply real SMTP credentials on the project owner's behalf.
- **Currency conversion is display-only.** `ExchangeRate` is a manual, admin-maintained per-tenant table with a `/convert` endpoint — there's no live FX API integration.
- **Only 3 fixed roles.** The permission system is policy-based under the hood, but there's no UI or API to assign a *custom* permission set to an individual user — every user is still Admin, Manager, or Cashier.
- **No CD to a real server.** CI publishes Docker images to GHCR on every push to `main`, but nothing pulls/deploys them anywhere — that needs a hosting decision (Azure/AWS/a VM/k8s) the project owner has to make.
- **Backup is a script, not a schedule.** `deploy/backup-database.ps1` exists and works for self-hosted SQL Server, but nothing runs it automatically — it needs to be wired into Task Scheduler/cron on whatever server actually hosts the database.
- **No staging/production `appsettings.*.json`** — only `appsettings.Development.json` exists; production config is expected to come entirely from environment variables/secrets manager, which is fine but undocumented as an explicit "staging" tier.
- **Caching is partial and single-instance.** `IMemoryCache` covers product categories only (not products/stock, deliberately, to avoid stale-inventory bugs); it wouldn't work correctly if PosFlow ever ran as more than one instance — that would need Redis or similar.
- **A known high-severity transitive vulnerability exists** in the test project's SQLite dependency (`SQLitePCLRaw.lib.e_sqlite3` 2.1.11, `GHSA-2m69-gcr7-jv3q`) — flagged by `dotnet list package --vulnerable` in CI but not currently blocking the build.
- **No load testing beyond a manual k6 script** — `tests/load/posflow-load-test.js` exists but isn't run in CI and hasn't been run against anything resembling production infrastructure.
- **No alerting** — `/metrics` exists for a self-hosted Prometheus/Grafana to scrape, but nothing is configured to page/notify anyone on an error spike or downtime.

---

## 6. Before going to production — must-do list

1. **Real SMTP credentials** (§5) — forgot-password logs a link instead of emailing it until `Smtp:Host` etc. are set.
2. **Rotate secrets** — `Jwt:Key` and the DB connection string ship as development placeholders in `appsettings.json`. Use environment variables or a secrets manager (Azure Key Vault wiring already exists — set `KeyVault:Uri`) in production; never commit real secrets.
3. **Set `Cors:AllowedOrigins`** in production config to your real frontend domain(s) — it ships empty on purpose (fails closed, not open).
4. **Set `App:FrontendBaseUrl`** — used to build the reset-password link.
5. **Point your host's health checks at `/health/live` and `/health/ready`** (separate liveness/readiness probes).
6. **Run migrations as an explicit deploy step** (`App:AutoMigrateOnStartup` defaults to `false` outside Development) — see `deploy/README.md`.
7. **Schedule the backup script** (or rely on your managed DB's automated backups) — see `deploy/README.md` §6.
8. **Choose a hosting target and add a deploy job** that pulls the GHCR images onto it — CI currently stops at "images published to GHCR".

---

## 7. Suggested next steps, roughly in order

1. Decide on a hosting target (Azure/AWS/a VM/k8s) and add a real deploy step to CI.
2. Wire up real SMTP credentials if forgot-password matters for your launch.
3. Schedule the backup script (or confirm your managed DB's automatic backups meet your retention needs).
4. Do one real manual walkthrough as a cashier, then as an admin, on the environment you're about to launch.
5. Point `/health/ready` and `/metrics` at real monitoring/alerting.
6. Only after it's live and being used for real — revisit §5 and decide which remaining limitations actually matter for your shop, rather than guessing in the abstract.
