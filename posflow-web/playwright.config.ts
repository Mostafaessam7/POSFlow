import { defineConfig, devices } from '@playwright/test';

/**
 * E2E tests drive the real running app end-to-end (real backend, real
 * SQL Server, real browser) - unlike the unit tests (`npm test`),
 * which mock everything below the component. See e2e/README.md for
 * how to run these locally; CI runs them against a real mssql service
 * container (.github/workflows/e2e.yml).
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // tests share one seeded backend/database state
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: process.env.CI ? 'github' : 'list',

  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:4200',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  },

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] }
    }
  ]
});
