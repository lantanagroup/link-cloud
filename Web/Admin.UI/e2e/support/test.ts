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
}

/**
 * Extended test for the mocked tier: every page starts with deterministic app config
 * and an /api catch-all. Live specs import { test } from '@playwright/test' directly.
 */
export const test = base.extend<E2eFixtures>({
  appConfig: [{}, { option: true }],
  allowUnmatchedApi: [false, { option: true }],
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
