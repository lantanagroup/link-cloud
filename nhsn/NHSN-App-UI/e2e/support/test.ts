import {test as base, expect} from '@playwright/test';
import {HarnessPage} from './pages/harness.page';
import {OnboardingPage} from './pages/onboarding.page';

/**
 * The mocked tier's `test`. Mirrors Web/Admin.UI's `e2e/support/test.ts`, minus the `/api`
 * interceptor: in mock mode `MockApiClient` implements the `ApiClient` port in-process, so
 * no request leaves the page and there is nothing to intercept or to leave unmatched.
 *
 * What does carry over is the console tripwire, which in the Admin.UI suite caught two real
 * bugs that were passing silently. It applies to every spec here for free.
 */

/** Errors every page load in the dev server produces, which say nothing about a spec. */
const IGNORED_CONSOLE_ERRORS: RegExp[] = [
  // A 404'd asset is reported twice; the fetch failure itself is never the interesting half.
  /^Failed to load resource:/,
  /favicon\.ico/,
  // `src/app/index.html` loads `/shell-config.js`, which only the Express runtime server
  // serves (`server/main.js` generates it from env/AAC). webpack-dev-server answers with
  // index.html instead, so Chrome refuses the script for its MIME type. Harmless in the
  // mocked tier — the file only carries default JWT settings, which mock mode never reads —
  // but it means `npm start` can never have a clean console. Deployed builds do serve it.
  /Refused to execute script from '[^']*\/shell-config\.js'/
];

export interface TestFixtures {
  harness: HarnessPage;
  onboarding: OnboardingPage;
  /** Set when the console output *is* the subject of the spec. */
  allowConsoleErrors: boolean;
  /** Extra patterns tolerated by one spec, on top of the list above. */
  allowedConsoleErrors: Array<string | RegExp>;
  consoleGuard: void;
}

export const test = base.extend<TestFixtures>({
  allowConsoleErrors: [false, {option: true}],
  allowedConsoleErrors: [[], {option: true}],

  harness: async ({page}, use) => {
    await use(new HarnessPage(page));
  },

  onboarding: async ({page}, use) => {
    await use(new OnboardingPage(page));
  },

  consoleGuard: [
    async ({page, allowConsoleErrors, allowedConsoleErrors}, use, testInfo) => {
      const collected = new Set<string>();
      const tolerated = [...IGNORED_CONSOLE_ERRORS, ...allowedConsoleErrors];
      const isTolerated = (text: string) =>
        tolerated.some((pattern) =>
          typeof pattern === 'string' ? text.includes(pattern) : pattern.test(text)
        );

      page.on('console', (message) => {
        if (message.type() !== 'error') {
          return;
        }
        const text = message.text();
        if (!isTolerated(text)) {
          collected.add(text);
        }
      });
      // An uncaught exception never reaches `console`, and is the more serious of the two.
      page.on('pageerror', (error) => {
        const text = `[pageerror] ${error.message}`;
        if (!isTolerated(text)) {
          collected.add(text);
        }
      });

      await use();

      // Only on a passing test: on a failure the console noise is usually the symptom, and
      // replacing the real failure with this one buries it.
      if (!allowConsoleErrors && collected.size > 0 && testInfo.status === testInfo.expectedStatus) {
        throw new Error(
          `The page logged ${collected.size} console error(s):\n  - ${[...collected].join('\n  - ')}\n` +
            'Fix the cause, or scope it with test.use({ allowedConsoleErrors: [...] }).'
        );
      }
    },
    {auto: true}
  ]
});

export {expect};
