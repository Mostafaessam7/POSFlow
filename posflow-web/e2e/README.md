# E2E tests (Playwright)

These drive the real running app in a real browser - the backend API,
a real SQL Server, and the Angular dev server all actually running,
unlike `npm test` (unit tests, everything mocked).

## Running locally

You need SQL Server reachable (a local instance, LocalDB, or
`docker compose up sql` from the repo root), then:

```bash
# Terminal 1 - backend, pointed at your SQL Server, auto-migrate +
# demo-seed on (Development environment does both by default)
cd src/PosFlow.Api
dotnet run

# Terminal 2 - frontend dev server
cd posflow-web
npm start

# Terminal 3 - the E2E tests themselves
cd posflow-web
npx playwright install --with-deps chromium   # first time only
npm run test:e2e
```

`playwright.config.ts` defaults to `http://localhost:4200` - override
with `E2E_BASE_URL` if you're pointing at somewhere else (a
docker-compose stack, a staging deploy, ...).

## Running in CI

`.github/workflows/e2e.yml` runs the same tests against a real `mssql`
service container - no LocalDB/Windows-auth quirks there, just a
plain SQL Server instance with SQL auth, which is the most portable
setup for a Linux CI runner.

## Why these are separate from the unit tests

`npm test` (Vitest) tests components/services in isolation with mocked
HTTP - fast, runs on every commit, but can't catch "the login page and
the auth API don't actually agree on a field name" class of bugs.
These E2E tests exist specifically to catch that class of bug, at the
cost of being slower and needing real infrastructure - so there are
intentionally few of them, covering only the highest-value paths
(login, and the core POS sale flow).
