import {expect, test} from '../support/test';
import {draftAt, facilityInfoComplete} from '../fixtures/drafts';

/**
 * FHIR Server Information — the step with the most validation, and the only one whose
 * completion depends on an API call having succeeded (`connectionTested`).
 */
test.describe('FHIR server information step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({
      draft: draftAt('fhir', {facilityInfo: facilityInfoComplete()}),
      path: '/onboarding/fhir'
    });
  });

  test('shows the vendor-specific authentication instructions', async ({page, onboarding}) => {
    // Vendor branching is field-level and driven by the VendorProfile the BFF serves, so this
    // copy is a function of the seeded draft's vendor (Epic), not of a code branch on a
    // vendor name. Seed Cerner and this paragraph changes.
    await expect(onboarding.stepPanel).toContainText('JSON Web Key Set (JWKS) based authentication');
    await expect(page.getByRole('button', {name: 'Download PDF Instructions'})).toBeVisible();
  });

  test('rejects a base URL that is not an http(s) URL', async ({page, onboarding}) => {
    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill('not-a-url');
    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(
      'Enter a valid URL starting with http:// or https:// (e.g. https://ehr.example.org/fhir/r4).'
    );
    await onboarding.expectOnStep('fhir');
  });

  test('requires throttle and lag values, not just the URL', async ({page, onboarding}) => {
    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill('https://ehr.example.org/fhir/r4');
    await onboarding.continueButton.click();

    // Max Concurrent Requests has no default, and the lag duration must be non-zero across
    // Days/Hours/Minutes combined — each is capped independently, but one must be set.
    const alerts = await onboarding.alertTexts();
    expect(alerts).toContain('Max Concurrent Requests is required.');
    expect(alerts).toContain('At least one of Days, Hours, or Minutes must be greater than zero.');
  });

  test('reports a successful connection test', async ({page, onboarding}) => {
    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill('https://ehr.example.org/fhir/r4');

    await page.getByRole('button', {name: 'Test Connection'}).click();

    // role=status, not role=alert — success is announced politely. MockApiClient succeeds for
    // any valid http(s) URL and marks the result simulated.
    await expect(onboarding.statuses).toContainText('Connection test succeeded.');
  });

  test('a connection test on an invalid URL does not report success', async ({page, onboarding}) => {
    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill('not-a-url');

    await page.getByRole('button', {name: 'Test Connection'}).click();

    // MockApiClient.testFhirConnection runs the same isValidHttpUrl the form does, so this is
    // the client-side guard, not a server verdict. Asserting the absence of the success copy
    // keeps the spec honest about which of the two it has actually observed.
    await expect(onboarding.stepPanel).not.toContainText('Connection test succeeded.');
  });

  test('the Days spinner carries a duplicated accessible name', async ({page}) => {
    // Characterisation of a real a11y defect, not an endorsement.
    //
    // Hours and Minutes are named plainly; Days is named "Patient Resource Acquisition Lag
    // Duration Patient Resource Acquisition Lag Duration Days". FhirStep.tsx:467 binds the
    // *group's* label to the Days input with `htmlFor="lagDays"`, and that label contains its
    // own text twice — once as text, once as the `InfoTooltip`'s `label` — so both copies land
    // in the name, ahead of NumberField's own "Days". Hours and Minutes carry no `htmlFor`,
    // which is why only Days is affected.
    //
    // The fix is to label the group with a plain element instead of a `<label htmlFor>` for
    // one of its three inputs. When that lands this test fails; assert the plain name then.
    await expect(page.getByRole('spinbutton', {name: 'Hours', exact: true})).toBeVisible();
    await expect(page.getByRole('spinbutton', {name: 'Minutes', exact: true})).toBeVisible();
    await expect(page.getByRole('spinbutton', {name: 'Days', exact: true})).toHaveCount(0);
    await expect(
      page.getByRole('spinbutton', {
        name: /Patient Resource Acquisition Lag Duration Patient Resource Acquisition Lag Duration Days/
      })
    ).toBeVisible();
  });
});
