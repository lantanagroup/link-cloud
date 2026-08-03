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

// Which monitor the `demo` project opens on. These are Windows virtual-desktop coordinates,
// so a screen above/left of the primary has negative values — list yours with:
//   Add-Type -AssemblyName System.Windows.Forms
//   [System.Windows.Forms.Screen]::AllScreens | Select DeviceName,Primary,Bounds
// Unset means "wherever Chromium wants".
const demoWindowPosition = process.env['E2E_WINDOW_POSITION'];
const [demoWidth, demoHeight] = (process.env['E2E_WINDOW_SIZE'] ?? '1600,1000')
  .split(',')
  .map((n) => Number(n.trim()));

export default defineConfig({
  testDir: './e2e',
  // Generous per-test budget: `ng serve` compiles each route the first time it is hit, so a
  // cold navigation can cost 10-15s on its own. The assertion timeout stays tight — a genuinely
  // broken expectation should still fail fast rather than idle out the whole test.
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  // Every mocked worker shares one `ng serve`, which compiles routes on demand. The default
  // (half the cores — 10 on a 20-core box) starves it: navigations stretch past 10s and the
  // app's main thread misses the expect window, surfacing as spurious "the click did nothing"
  // failures. Cap the fan-out to what one dev server can feed.
  workers: process.env['CI'] ? 2 : 4,
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
    {
      // Not a verification tier — a guided walkthrough for watching the app drive itself.
      // One visible browser, paced by slowMo, and no timeout so it can sit on page.pause()
      // at the end with the window still open. Excluded from `npm run e2e`, so CI never
      // blocks on it.
      name: 'demo',
      testDir: './e2e/demo',
      timeout: 0,
      use: {
        baseURL: 'http://localhost:4200',
        headless: false,
        // Explicit viewport rather than `viewport: null` — null is rejected alongside the
        // deviceScaleFactor that the Desktop Chrome preset brings in. Playwright sizes the
        // real window to fit this, so --window-size is unnecessary; only position is passed.
        viewport: { width: demoWidth, height: demoHeight },
        launchOptions: {
          slowMo: Number(process.env['E2E_SLOW_MO'] ?? 700),
          args: demoWindowPosition ? [`--window-position=${demoWindowPosition}`] : [],
        },
        trace: 'off',
        video: 'off',
      },
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
