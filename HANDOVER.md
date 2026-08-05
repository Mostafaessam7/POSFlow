# PosFlow — Handover Document

**Last updated:** 28 July 2026
**Prepared by:** Claude (Anthropic), across an extended pairing session with the project owner.

This document is the single reference for anyone picking up PosFlow after this session — what it is, what's been built, how to apply the work, what's still missing, and what to do first.

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

**If any of these zips have been lost**, they'll need to be regenerated — this document alone isn't sufficient to reconstruct the code, only to understand what exists and why.

---

## 3. Current feature set

### Backend APIs (all under `/api/`)
| Area | Endpoints | Notes |
|---|---|---|
| Auth | `login`, `refresh`, `logout`, `forgot-password`, `reset-password`, `me` | Rotating refresh tokens; forgot-password needs a real email provider wired in (see §6) |
| Products | full CRUD + pagination + category filter | RowVersion optimistic concurrency enforced on update |
| Categories | full CRUD | Delete blocked if products still reference it |
| Orders | `checkout`, `by-shift/{id}`, `{id}` (get), `{id}/void` | Stock-aware, split payments, void restores stock |
| Shifts | `open`, `{id}/close`, `current`, `history`, `branch-history` | `branch-history` is Admin/Manager only |
| Users | full CRUD + `reset-password` | Admin only |
| Branches | full CRUD | Admin only |
| Reports | `daily-summary` | Admin/Manager only; today's sales, cash/card split, top products |
| — | `/health` | Checks DB connectivity |

### Frontend screens
Login, Forgot/Reset Password, Open/Close Shift, POS Checkout, Product List, Shift History, Sales Dashboard, Users admin, Branches admin.

### Cross-cutting
- Global exception handler (consistent error JSON shape, no more per-controller try/catch)
- FluentValidation on every request DTO, enforced by a global action filter
- CORS (configurable allow-list)
- Pagination on every list endpoint (`page`/`pageSize` query params)
- A cohesive visual design system (CSS custom properties, Tajawal + JetBrains Mono type, warm "receipt paper" palette)

### Tests
- `tests/PosFlow.Application.Tests` — unit tests for Shift/Order/Product service logic and validators (EF Core InMemory)
- `tests/PosFlow.Api.Tests` — integration tests hitting the real HTTP pipeline for every controller (WebApplicationFactory)
- Frontend: guard tests, checkout cart/payment-math tests, and a fix to a previously-broken default Angular scaffold test
- CI (`.github/workflows/ci.yml`) runs all of the above on every push/PR to `main`

---

## 4. Setup checklist (do this first)

None of this code has been compiled or run — it was all written without access to a .NET SDK or Node in this session. **Treat it as "should work" code that needs verification, not "known working" code.**

1. Extract all 12 zips in order (§2), each into the location its `README.txt` specifies.
2. Run the migrations mentioned in the READMEs for batches 6, 9 (`AddRefreshTokens`... actually check each batch — batches 5, 6, and 12 each add DB changes), in order:
   - Batch 5: `RefreshTokens` table
   - Batch 6: Product stock/category columns, Order void columns, `ProductCategories` table
   - Batch 12: `AppUser.Email` column, `PasswordResetTokens` table
   - Generate these as **one combined migration** if you haven't been applying them incrementally — don't need three separate migrations if you're starting fresh.
3. `dotnet build` the solution and fix any compile errors (there's a real chance of small ones — namespace typos, a missed usage update after a signature change, etc. — see §7).
4. `dotnet test` — get the test suite green.
5. `npm install` in `posflow-web/`, then `ng build` — fix any compile errors there too.
6. `ng test` — get the frontend suite green.
7. Run both apps together and walk through one full manual flow yourself: login → open shift → sell something → close shift → check the dashboard.

---

## 5. Known limitations (honest list)

These were explicitly identified and deliberately deferred, not overlooked:

- **Email is not actually sent.** `LoggingEmailSender` (the default `IEmailSender`) logs the reset link instead of emailing it. Replace the DI registration in `Program.cs` with a real provider (SendGrid, SES, SMTP/MailKit) before relying on forgot-password in production.
- **No receipt printing/PDF export** — the checkout success card is on-screen only.
- **No barcode-scanner-optimized lookup endpoint** — a scanner that types a barcode + Enter would currently just filter the in-memory product list client-side, which is fine at small catalog sizes but not built for scale.
- **No order-level discounts or tax configuration** — discounts are per-line only; tax is hardcoded to 0.
- **No customer records** — every sale is anonymous.
- **Stock has no adjustment/audit trail** — `StockQuantity` is just a number editors can overwrite directly, no "received 50 units on X" history.
- **No E2E browser tests** (Playwright/Cypress) — all test coverage is unit + API-integration level, nothing clicks through the actual rendered UI.
- **Order-number collision retry logic is untested** — hard to force a genuine unique-constraint race deterministically against EF Core's InMemory provider, so this path (real, but rare) has no automated test.

---

## 6. Before going to production — must-do list

1. **Real email provider** (§5) — forgot-password is non-functional without this.
2. **Rotate secrets** — `Jwt:Key` and the DB connection string in `appsettings.json` are development placeholders. Use environment variables or a secrets manager in production; never commit real secrets.
3. **Set `Cors:AllowedOrigins`** in production `appsettings.json` to your real frontend domain(s) — it ships empty on purpose (fails closed, not open).
4. **Set `App:FrontendBaseUrl`** in production `appsettings.json` — used to build the reset-password link.
5. **Point your host's health checks at `/health`.**
6. Decide on a real backup/retention policy for the SQL Server database — not something this session touched at all.

---

## 7. If the build doesn't compile cleanly

This is the single most likely thing to go wrong, precisely because none of this was compiler-checked. If you hit errors:

- Most likely causes: a `using` that should've been added wasn't, a DTO constructor's parameter order changed in a later batch and one call site elsewhere wasn't updated, or a namespace mismatch.
- These are almost always quick, mechanical fixes once you can see the actual compiler error — paste it back into a conversation with Claude and it can be fixed in a turn or two, much faster than trying to debug it blind.
- If you're using Claude Code or a similar coding agent locally, it can iterate against the real compiler directly, which will be faster than round-tripping through chat for this kind of cleanup.

---

## 8. Suggested next steps, roughly in order

1. Get it compiling and running (§4).
2. Fix whatever the compiler/test run surfaces.
3. Do one real manual walkthrough as a cashier, then as an admin.
4. Wire up a real email provider if forgot-password matters to you.
5. Deploy to a real environment and point `/health` at your monitoring.
6. Only after it's live and being used for real — revisit §5 and decide which limitations actually matter for your shop, rather than guessing in the abstract.
