import { Page } from '@playwright/test';

/**
 * Mirror of the runtime config shape in src/app/services/app-config.service.ts.
 * Kept structural (not imported) because that file lives in the Angular compilation
 * unit; the fields the e2e tier controls are stable.
 */
export interface AppConfigOverrides {
  authRequired?: boolean;
  baseApiUrl?: string;
  allowAlphaNumericFacilityId?: boolean;
  oauth2?: { enabled: boolean };
  kafkaUrl?: string;
  grafanaUrl?: string;
}

/**
 * The app deep-merges /assets/app.config.local.json (git-ignored, developer-specific)
 * over /assets/app.config.json. Intercepting the local file makes mocked runs
 * deterministic regardless of what the developer has on disk, and is also how tests
 * opt back INTO auth (authRequired: true) to exercise the guard.
 */
export async function injectAppConfig(page: Page, overrides: AppConfigOverrides = {}): Promise<void> {
  await page.route('**/assets/app.config.local.json', (route) =>
    route.fulfill({
      json: {
        authRequired: false,
        baseApiUrl: '/api',
        allowAlphaNumericFacilityId: true,
        oauth2: { enabled: false },
        ...overrides,
      },
    }),
  );
}
