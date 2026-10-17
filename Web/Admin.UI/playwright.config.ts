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
  // The Desktop Chrome preset is applied per project rather than here: it carries a
  // deviceScaleFactor, and Playwright refuses that alongside the `viewport: null` the demo
  // project needs. Setting it to undefined in the project does not help — the key is still
  // present once the objects merge.
  use: {
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'mocked',
      testDir: './e2e/mocked',
      use: { ...devices['Desktop Chrome'], baseURL: 'http://localhost:4200' },
    },
    {
      name: 'live',
      testDir: './e2e/live',
      use: { ...devices['Desktop Chrome'], baseURL: liveBaseUrl },
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
        // `timeout: 0` above removes the test timeout so the walkthrough can sit on the open
        // window at the end. On its own that also means an action waiting on something that
        // never appears waits forever, so a stale selector hangs the demo instead of failing
        // it — which has cost real time twice. Generous enough for a cold ng serve compiling
        // a route on first visit, short enough that a break is obvious.
        actionTimeout: 30_000,
        // The demo facility is created in America/Denver, and the ad-hoc form converts the
        // period the presenter types from the facility's zone into UTC. The report screen then
        // renders it back in the *browser's* zone, so without pinning this the reporting
        // period reads differently on every presenter's machine — and can land on the wrong
        // day either side of midnight. Matching the facility makes the dates on screen the
        // dates that were typed, wherever the demo is given from.
        timezoneId: 'America/Denver',
        // No viewport emulation: the page is whatever size the real window is.
        //
        // A fixed viewport also fakes `window.screen`, and the demo asks the browser which
        // monitors exist in order to place itself. With emulation on, the screen it is
        // currently displayed on comes back reporting the viewport's dimensions instead of
        // the monitor's, so the window was sized and centred against 1600x1000 and landed
        // small in the corner of the laptop. Switching it off is what makes the screen data
        // true. deviceScaleFactor has to go with it — Playwright rejects the pair, and the
        // Desktop Chrome preset supplies one — so the zoom is applied as a Chrome flag below.
        viewport: null,
        launchOptions: {
          // Presentation pacing by default. This project exists to be watched by a room, so
          // the common case should need no environment variables at all; 700 was fast enough
          // that the walkthrough finished before anyone could talk over it. Drop it to 0 when
          // you only want to know whether the walkthrough still passes.
          slowMo: Number(process.env['E2E_SLOW_MO'] ?? 2200),
          args: [
            // Browser zoom, off by default. It enlarges the UI rather than the window, and
            // the screen API reports the scaled size too, so the window still fills the
            // monitor. Tempting for a large room, but it costs vertical space where this
            // app can least afford it: at 1.25 on a 27" 1440p screen the facility page ends
            // at the Weekly field, with all four configuration panels — the things the
            // walkthrough expands — pushed off the bottom. Raise it only if the room is big
            // enough that legibility matters more than seeing a screen whole.
            `--force-device-scale-factor=${process.env['E2E_ZOOM'] ?? '1'}`,
            // Born off the virtual desktop, then moved onto the chosen screen once the page
            // can tell us where the screens are. The window cannot be launched in the right
            // place, because the coordinate is not knowable until the browser is running —
            // so the choice is a flash on the laptop or a moment of nothing. The helper
            // always moves it somewhere visible, including when detection fails.
            `--window-position=${demoWindowPosition ?? '-32000,-32000'}`,
          ],
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
