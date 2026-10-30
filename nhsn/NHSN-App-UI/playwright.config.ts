import {defineConfig, devices} from '@playwright/test';

/**
 * Two tiers, mirroring Web/Admin.UI's suite:
 *
 * - `mocked` (default) drives the standalone shell in its own **Mock (offline)** mode, so
 *   `MockApiClient` serves the whole `ApiClient` port in-process. There is nothing on the
 *   wire to intercept, which is why this suite has no `/api` route interceptor: determinism
 *   comes from the mock client plus a seeded draft, not from fixtures on HTTP.
 * - `bff` is opt-in and needs NHSN-App-BFF on :8079 plus a signed-JWT profile. Its preflight
 *   fails fast rather than letting a spec time out against a dead port.
 *
 * `npm start` is webpack-dev-server on **4300** (not Angular's 4200) and, like `ng serve`,
 * answers on `localhost` but not on the `127.0.0.1` literal — so the base URL uses the
 * hostname. A cold compile is ~30-60s, hence the generous webServer timeout.
 */
const BASE_URL = process.env.NHSN_E2E_BASE_URL ?? 'http://localhost:4300';

export default defineConfig({
  testDir: './e2e',
  // Playwright transpiles without typechecking; `npm run typecheck:e2e` is what catches
  // drift between these specs and src/ (tsconfig.json covers src only).
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 4,
  reporter: process.env.CI ? [['github'], ['html', {open: 'never'}]] : [['list'], ['html', {open: 'never'}]],
  timeout: 60_000,
  expect: {timeout: 10_000},

  use: {
    ...devices['Desktop Chrome'],
    baseURL: BASE_URL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'off'
  },

  projects: [
    {
      name: 'mocked',
      testMatch: /mocked\/.*\.spec\.ts$/
    },
    {
      name: 'bff',
      testMatch: /bff\/.*\.bff\.spec\.ts$/
    }
  ],

  webServer: {
    command: 'npm start',
    url: BASE_URL,
    reuseExistingServer: true,
    timeout: 240_000,
    stdout: 'ignore',
    stderr: 'pipe'
  }
});
