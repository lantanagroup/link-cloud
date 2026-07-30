import { test as base, expect } from '@playwright/test';
import { ApiMock } from './api-mock';
import { AppConfigOverrides, injectAppConfig } from './app-config';

export interface E2eFixtures {
  /** Per-spec app config overrides; set via test.use({ appConfig: { authRequired: true } }). */
  appConfig: AppConfigOverrides;
  /** Auto-installed /api interceptor with 404 catch-all. Register fixtures via api.mock(...). */
  api: ApiMock;
  /** Opt-out for specs that intentionally leave endpoints unmocked. */
  allowUnmatchedApi: boolean;
  /** Console errors seen during the test, allowlist already applied. Assertable by specs. */
  consoleErrors: string[];
  /** Opt-out for specs that deliberately drive the app into a logged-error state. */
  allowConsoleErrors: boolean;
  /** Extra ignore patterns for this spec, on top of the built-in noise list. */
  allowedConsoleErrors: (string | RegExp)[];
}

/**
 * Noise that says nothing about the app's own health:
 * - resource-load failures are the expected shape of any mocked non-2xx, including the
 *   catch-all 404 — genuinely missing fixtures are already caught by `api.unmatched`
 * - ngx-toastr renders handled errors as toasts; specs assert the toast, not the log
 */
const IGNORED_CONSOLE_ERRORS: RegExp[] = [/^Failed to load resource:/];

const firstLine = (text: string): string => text.split('\n')[0].trim();

/**
 * Extended test for the mocked tier: every page starts with deterministic app config
 * and an /api catch-all. Live specs import { test } from '@playwright/test' directly.
 */
export const test = base.extend<E2eFixtures>({
  appConfig: [{}, { option: true }],
  allowUnmatchedApi: [false, { option: true }],
  allowConsoleErrors: [false, { option: true }],
  allowedConsoleErrors: [[], { option: true }],
  /**
   * Tripwire for errors the app logs but the UI swallows — an Angular lifecycle throw leaves
   * the page looking fine, so assertions pass while the console fills up. Collects
   * console.error plus uncaught exceptions, and fails the test naming them.
   */
  consoleErrors: [
    async ({ page, allowedConsoleErrors, allowConsoleErrors }, use, testInfo) => {
      const ignored = [...IGNORED_CONSOLE_ERRORS, ...allowedConsoleErrors];
      const ignore = (text: string): boolean =>
        ignored.some((p) => (typeof p === 'string' ? text.includes(p) : p.test(text)));

      const errors: string[] = [];
      const record = (text: string): void => {
        const line = firstLine(text);
        // The same lifecycle error re-fires on every change detection pass; one entry is enough.
        if (!ignore(text) && !errors.includes(line)) errors.push(line);
      };

      page.on('console', (msg) => {
        if (msg.type() === 'error') record(msg.text());
      });
      page.on('pageerror', (err) => record(`Uncaught ${err.message}`));

      await use(errors);

      // Don't pile on when the test already failed — the first failure is the useful one.
      if (!allowConsoleErrors && testInfo.errors.length === 0) {
        expect(
          errors,
          'console errors during this test — fix the app, or opt out with test.use({ allowConsoleErrors: true }) / allowedConsoleErrors',
        ).toEqual([]);
      }
    },
    { auto: true },
  ],
  api: [
    async ({ page, appConfig, allowUnmatchedApi }, use) => {
      await injectAppConfig(page, appConfig);
      const api = new ApiMock(page);
      await api.install();
      await use(api);
      // Every unmocked /api call surfaced a 404 (and usually an error toast) in the app.
      // Fail loudly and name the endpoints, so missing fixtures never pass silently.
      if (!allowUnmatchedApi) {
        expect(
          api.unmatched.map((c) => `${c.method} ${new URL(c.url).pathname}`),
          'unmocked /api calls during this test — add fixtures via api.mock(...) or opt out with test.use({ allowUnmatchedApi: true })',
        ).toEqual([]);
      }
    },
    { auto: true },
  ],
});

export { expect };
