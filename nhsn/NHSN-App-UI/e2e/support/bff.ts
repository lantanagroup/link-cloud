import {expect, type Page, type APIRequestContext} from '@playwright/test';

/**
 * Preflight for the `bff` tier. Fails fast and says what is missing, rather than letting a
 * spec spend its 60s timing out against a closed port.
 *
 * The tier is deliberately thin. Two things have to be true before it can be more than a
 * smoke test, and neither is yet:
 *
 * 1. **The BFF's onboarding endpoints do not exist.** `NHSN-App-UI/README.md` is explicit
 *    that mock mode is the only way any step runs until the BFF's phase 1 lands. A draft
 *    spec against them would be a spec against a fiction.
 * 2. **BFF mode needs a signed-JWT profile** — issuer, `kid` and PKCS#8 private key matching
 *    the BFF's `NhsnJwt` configuration. `docker-compose.yml` supplies those as
 *    `NHSN_APP_UI_DEFAULT_JWT_*` to the shell's runtime server, which serves them at
 *    `/shell-config.js`; the webpack dev server does not serve that file at all. So a BFF
 *    run wants the composed shell (:8090), not `npm start`.
 */
export const BFF_BASE_URL = process.env.NHSN_BFF_BASE_URL ?? 'http://localhost:8079';

export async function assertBffReachable(request: APIRequestContext): Promise<void> {
  let response;
  try {
    response = await request.get(`${BFF_BASE_URL}/health`, {timeout: 5_000});
  } catch (error) {
    throw new Error(
      `NHSN-App-BFF is not reachable at ${BFF_BASE_URL} (${(error as Error).message}).\n` +
        'Start it per nhsn/documentation/SERVICES.md, or run the mocked tier instead: npm run e2e'
    );
  }
  expect(
    response.ok(),
    `NHSN-App-BFF answered ${response.status()} from ${BFF_BASE_URL}/health`
  ).toBeTruthy();
}

/**
 * The shell signs its JWT in the browser from the active profile, so a BFF-tier spec must
 * seed one before the app mounts. Keys match `src/shell/auth/test-user-storage.ts`.
 */
export async function seedTestProfile(
  page: Page,
  profile: {
    facilityId: string;
    facilityName?: string;
    email: string;
    name: string;
    groups: string[];
    issuer: string;
    keyId: string;
    privateKeyPem: string;
  }
): Promise<void> {
  const stored = {
    id: 'e2e-profile',
    label: 'e2e',
    facilityName: '',
    lastUsedOn: new Date().toISOString(),
    ...profile
  };
  await page.addInitScript((value) => {
    window.localStorage.setItem('nhsn-app-ui.testUsers', JSON.stringify([value]));
    window.localStorage.setItem('nhsn-app-ui.activeTestUserId', 'e2e-profile');
  }, stored);
}
