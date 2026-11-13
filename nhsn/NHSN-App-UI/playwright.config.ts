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
 * - `demo` is not a verification tier at all — one visible, slowMo-paced browser walking the
 *   whole of onboarding for somebody to watch, and a `.webm` of the run to send to somebody
 *   who cannot. Excluded from `npm run e2e`, so CI never blocks on it.
 *
 * `npm start` is webpack-dev-server on **4300** (not Angular's 4200) and, like `ng serve`,
 * answers on `localhost` but not on the `127.0.0.1` literal — so the base URL uses the
 * hostname. A cold compile is ~30-60s, hence the generous webServer timeout.
 */
const BASE_URL = process.env.NHSN_E2E_BASE_URL ?? 'http://localhost:4300';

/**
 * `demo` knobs. Position is Windows virtual-desktop coordinates, so a monitor above or left of
 * the primary has negative values — list yours with:
 *   Add-Type -AssemblyName System.Windows.Forms
 *   [System.Windows.Forms.Screen]::AllScreens | Select DeviceName,Primary,Bounds
 * Unset means "wherever Chromium wants".
 */
const demoWindowPosition = process.env.E2E_WINDOW_POSITION;
const [demoWidth, demoHeight] = (process.env.E2E_WINDOW_SIZE ?? '1600,1000')
  .split(',')
  .map((value) => Number(value.trim()));

export default defineConfig({
  testDir: './e2e',
  // Playwright transpiles without typechecking; `npm run typecheck:e2e` is what catches
  // drift between these specs and src/ (tsconfig.json covers src only).
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 4,
  // The junit file is what the CI job hands to dorny/test-reporter, so a failure shows up as
  // annotated test names rather than only in the raw log.
  reporter: process.env.CI
    ? [['github'], ['junit', {outputFile: 'test-results/junit.xml'}], ['html', {open: 'never'}]]
    : [['list'], ['html', {open: 'never'}]],
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
    },
    {
      name: 'demo',
      testMatch: /demo\/.*\.demo\.spec\.ts$/,
      // No test timeout, so the walkthrough can sit on the finished window at the end.
      timeout: 0,
      use: {
        headless: false,
        // `timeout: 0` above also removes the ceiling on an action waiting for something that
        // never appears, which would hang the demo instead of failing it. Generous enough for a
        // cold webpack compile on the first navigation, short enough that a break is obvious.
        actionTimeout: 30_000,
        // An explicit viewport, not `viewport: null` — null is rejected alongside the
        // deviceScaleFactor the Desktop Chrome preset brings in. Playwright sizes the real window
        // to fit this, so only the position has to be passed to Chromium.
        viewport: {width: demoWidth, height: demoHeight},
        launchOptions: {
          slowMo: Number(process.env.E2E_SLOW_MO ?? 700),
          args: demoWindowPosition ? [`--window-position=${demoWindowPosition}`] : []
        },
        trace: 'off',
        // On by default here, unlike the other two tiers: the point of this tier is to be
        // watched, and the recording is how somebody watches it who was not at the machine.
        // `E2E_VIDEO=off` skips it. The file lands under test-results/ and the run prints it.
        video: process.env.E2E_VIDEO === 'off' ? 'off' : 'on'
      }
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
