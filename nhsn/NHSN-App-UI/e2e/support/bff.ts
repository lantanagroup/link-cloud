import {expect, type Page, type APIRequestContext} from '@playwright/test';

/**
 * Preflight for the `bff` tier. Fails fast and says what is missing, rather than letting a
 * spec spend its 60s timing out against a closed port.
 *
 * The tier is thin, but **not** because the endpoints are missing. An earlier version of this
 * comment said they were, repeating `NHSN-App-UI/README.md`, which is stale: phase 1 landed in
 * LEGPROG-515 (`9771759e9`, 2026-10-23) and `OnboardingEndpoints` maps the draft read and
 * write, import, export and completion under `/api/nhsn-app-bff/onboarding`. Treat that README
 * line as out of date rather than as a reason not to write specs here.
 *
 * What is actually in the way is the credential, and it is a one-off cost rather than a
 * blocker: **BFF mode needs a signed-JWT profile** — issuer, `kid` and PKCS#8 private key
 * matching the BFF's `NhsnJwt` configuration. `docker-compose.yml` supplies those as
 * `NHSN_APP_UI_DEFAULT_JWT_*` to the shell's runtime server, which serves them at
 * `/shell-config.js`; the webpack dev server does not serve that file at all. So a BFF run
 * wants the composed shell (:8090), not `npm start`. `seedTestProfile()` below is the other
 * half, for a spec that drives the UI rather than the API directly.
 *
 * A BFF run may also point at a shared lower environment, so specs here stay read-only and
 * shape-based until that profile is wired up.
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
