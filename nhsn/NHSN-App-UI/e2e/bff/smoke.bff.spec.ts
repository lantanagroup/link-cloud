import {expect, test} from '@playwright/test';
import {BFF_BASE_URL, assertBffReachable} from '../support/bff';

/**
 * The `bff` tier's only spec, and intentionally a smoke test — see e2e/support/bff.ts for
 * why it cannot yet be more. Run with `npm run e2e:bff`; it is not part of `npm run e2e`.
 *
 * Shape-based assertions only, and nothing is created or deleted: a BFF run may point at a
 * shared lower environment.
 */
test.describe('NHSN-App-BFF smoke', () => {
  test('the BFF is up and reports healthy status', async ({request}) => {
    await assertBffReachable(request);

    const response = await request.get(`${BFF_BASE_URL}/health`);
    expect(response.ok()).toBeTruthy();
  });

  test('rejects an unauthenticated onboarding request', async ({request}) => {
    await assertBffReachable(request);

    // The component never sets an Authorization header itself — the NHSN gateway injects the
    // JWT in transit — so an unauthenticated call is the one contract the UI never exercises
    // and the one most worth pinning: it must not be treated as an anonymous read.
    //
    // It has to be the real route, and 404 must not be accepted. An unmatched path is rejected
    // by routing before authorization ever runs, so a request to a route that does not exist
    // returns 404 whether or not the endpoints are protected at all. This is the draft read
    // itself: `OnboardingEndpoints` maps GET "/" on a group carrying
    // `.RequireAuthorization("AuthenticatedUser")`. `Authentication:AllowAnonymous`, true in
    // the Development and Docker settings, does not loosen this — it governs the BFF's own
    // outbound calls to Link services, not its endpoints.
    const response = await request.get(`${BFF_BASE_URL}/api/nhsn-app-bff/onboarding`, {
      failOnStatusCode: false
    });
    expect([401, 403]).toContain(response.status());
  });
});
