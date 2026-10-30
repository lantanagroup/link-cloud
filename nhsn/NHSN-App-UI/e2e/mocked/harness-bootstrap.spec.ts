import {expect, test} from '../support/test';
import {MOCK_FACILITY_ID, MOCK_FACILITY_NAME} from '../fixtures/drafts';

/**
 * Getting into the app at all. Everything else in the tier depends on this working, so it is
 * asserted once here rather than implicitly in twenty specs.
 */
test.describe('harness bootstrap', () => {
  test('gates the wizard behind a test profile until mock mode is chosen', async ({page, harness}) => {
    await page.goto('/');

    // Mode defaults to BFF (FacilityHarness.tsx), and BFF mode needs a signed-JWT profile,
    // so the shell renders no client at all. The README calls mock mode the default; the
    // code does not. If this ever starts showing the wizard, that gap has been closed and
    // `HarnessPage.open()` can stop flipping the selector.
    await expect(harness.profileGate.getByRole('heading')).toHaveText('Select or create a test profile');
    await expect(harness.stepRail).toBeHidden();
  });

  test('mock mode mounts the wizard and mirrors the first step to the URL', async ({harness, onboarding}) => {
    await harness.open();

    // navigation.ts writes the step to the URL on mount — the app arrives at `/` and
    // corrects itself rather than waiting for a navigation.
    await onboarding.expectOnStep('welcome');
    await expect(onboarding.currentStepLabel()).toHaveText('Welcome');
  });

  test('shows the thirteen steps of the flow, in order', async ({harness, onboarding}) => {
    await harness.open();

    // The POC's order, with the facility picker removed. Labels are the visible text; the
    // accessible names differ for step 8 (see support/fields.ts on AcronymText).
    await expect(onboarding.stepLabels()).toHaveText([
      'Welcome',
      'Facility Reporting Plan',
      'Facility Information',
      'Manual Upload',
      'FHIR Server Information',
      'Patients of Interest Configuration',
      'Organization Identification',
      // textContent, which is what toHaveText compares, carries AcronymText's
      // visually-hidden spelling as well: 'Location Identification (HSLOCH S L O C)'.
      /^Location Identification \(HSLOC/,
      'Encounter Mapping',
      'Generate Test Report',
      'Report Results',
      'MRN Identifier Intake',
      'Enrollment Complete'
    ]);
  });

  test('identifies the simulated facility, never a real one', async ({harness}) => {
    await harness.open();

    // MockApiClient's defaults. Everything it invents is deliberately synthetic so a
    // lower-environment screenshot can't be mistaken for real facility data — if these ever
    // read like a real site, that property has been lost.
    await expect(harness.facilityName).toHaveText(MOCK_FACILITY_NAME);
    await expect(harness.facilityId).toHaveText(MOCK_FACILITY_ID);
  });
});
