import { defineConfig, devices } from '@playwright/test';

/**
 * Two tiers:
 *  - mocked: default; runs against `ng serve` (auto-started via webServer) with all /api
 *    traffic intercepted by fixtures. Fast, deterministic, no backend needed.
 *  - live: defaults to the docker compose admin-ui at http://localhost:8066, which calls
 *    the BFF cross-origin at http://localhost:8063/api (same setup CI uses). To run live
 *    against ng serve instead, set UI_E2E_BASE_URL=http://localhost:4200 and have a local
 *    BFF on 5218 (see proxy.conf.json).
 */
const externalBaseUrl = process.env['UI_E2E_BASE_URL'];
const liveBaseUrl = externalBaseUrl ?? 'http://localhost:8066';
// `ng serve` is only needed when the mocked project runs, or when live explicitly
// targets 4200 — not for the default live run against the compose container.
const liveOnly = /--project[= ]live/.test(process.argv.join(' '));
const needsDevServer = !liveOnly || liveBaseUrl.includes(':4200');

export default defineConfig({
  testDir: './e2e',
  timeout: 30_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: process.env['CI']
    ? [['list'], ['html', { open: 'never' }], ['junit', { outputFile: 'test-results/junit.xml' }]]
    : [['list'], ['html', { open: 'on-failure' }]],
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    ...devices['Desktop Chrome'],
  },
  projects: [
    {
      name: 'mocked',
      testDir: './e2e/mocked',
      use: { baseURL: 'http://localhost:4200' },
    },
    {
      name: 'live',
      testDir: './e2e/live',
      use: { baseURL: liveBaseUrl },
    },
  ],
  webServer: needsDevServer
    ? {
        command: 'npm start',
        url: 'http://localhost:4200',
        reuseExistingServer: true,
        timeout: 180_000,
      }
    : undefined,
});
