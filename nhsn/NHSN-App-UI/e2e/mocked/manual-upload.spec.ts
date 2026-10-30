import {expect, test} from '../support/test';
import {draftAt, facilityInfoComplete} from '../fixtures/drafts';

/**
 * Manual Upload Option — the offline route through onboarding. The step is optional by
 * design (`isComplete: () => true` in flow.ts), so nothing here gates Continue.
 */
test.describe('manual upload step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({
      draft: draftAt('manual-upload', {facilityInfo: facilityInfoComplete()}),
      path: '/onboarding/manual-upload'
    });
  });

  test('offers the import sheet download and the upload control', async ({page, onboarding}) => {
    await expect(onboarding.heading).toContainText('Manual Upload Option');
    await expect(page.getByRole('button', {name: 'Download Import Sheet & Instructions'})).toBeVisible();
    await expect(page.getByRole('button', {name: 'Choose File'})).toBeVisible();
  });

  test('surfaces the download as unavailable offline, without breaking the page', async ({page, onboarding}) => {
    await page.getByRole('button', {name: 'Download Import Sheet & Instructions'}).click();

    // MockApiClient.exportDraft throws on purpose — the import package is assembled by the
    // BFF from the facility's vendor, so there is nothing offline to serve. The thrown
    // Error's own message is what the user is shown, verbatim, prefixed by the component's
    // visually-hidden "Alert:".
    //
    // That copy names a "live mode" the UI does not have: the harness selector offers
    // "NHSN-App-BFF (:8079)" and "Mock (offline)". Worth fixing in MockApiClient; this
    // assertion is deliberately on the sentence a user would read, so it fails when it is.
    // The step renders two empty `role=alert` live regions up front, so a bare
    // `expect(alerts).toContainText(...)` is a strict-mode violation rather than a wait —
    // and strict-mode violations are not retried. Poll the collected text instead.
    await expect
      .poll(async () => (await onboarding.alertTexts()).join(' '))
      .toContain('The import package is served by the BFF. Switch the harness to live mode to download it.');

    // The failure is caught, not thrown into the console — the console tripwire in
    // support/test.ts would fail this test if it were not.
    await expect(page.getByRole('button', {name: 'Choose File'})).toBeVisible();
  });

  test('lets the user skip the step entirely', async ({onboarding}) => {
    await onboarding.continueButton.click();

    // Optional by design: its own copy invites uploading a partial form and finishing later.
    await onboarding.expectOnStep('fhir');
  });
});
