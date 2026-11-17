import {expect, test} from '../support/test';
import {ACRONYM_SPELLED, chooseOption, selectedOptionText} from '../support/fields';
import {draftAt} from '../fixtures/drafts';

/**
 * The first step with required fields, and the suite's reference example for the two Kendo
 * traps: a `Select` is not a `<select>`, and `AcronymText` rewrites accessible names.
 */
test.describe('facility information step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: draftAt('facility-info'), path: '/onboarding/facility-info'});
  });

  test('refuses to continue with nothing chosen, and says why per field', async ({onboarding}) => {
    // Continue is *not* disabled on an incomplete form — validation runs on click. A spec
    // asserting `toBeDisabled` here would fail, and wrongly suggest the gate is the button.
    await expect(onboarding.continueButton).toBeEnabled();

    await onboarding.continueButton.click();

    // Exact copy, as rendered. Note the alerts spell EHR normally — only accessible *names*
    // carry the letter-by-letter form.
    expect(await onboarding.alertTexts()).toEqual([
      'Facility Time Zone is required.',
      'EHR Vendor is required.',
      'Please complete all fields before continuing.'
    ]);
    await onboarding.expectOnStep('facility-info');
  });

  test('clears a field error as soon as that field is answered', async ({page, onboarding}) => {
    await onboarding.continueButton.click();
    await expect(onboarding.alerts).toHaveCount(3);

    await chooseOption(page, 'facilityEhrVendor', 'Epic');

    // The vendor error goes; the untouched time zone's error stays, along with the summary.
    expect(await onboarding.alertTexts()).toEqual([
      'Facility Time Zone is required.',
      'Please complete all fields before continuing.'
    ]);
  });

  test('advances once both fields are chosen, and records them in the draft', async ({page, harness, onboarding}) => {
    await chooseOption(page, 'facilityEhrVendor', 'Epic');
    // The option label is `America/New_York — (UTC-04:00) Eastern Time`, and that offset is
    // computed from the current date: it reads UTC-05:00 outside daylight saving. Match the
    // IANA id, which is also the value the draft stores, and never the rendered offset.
    await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);

    await expect(selectedOptionText(page, 'facilityEhrVendor')).toHaveText('Epic');
    await expect(page.getByRole('combobox', {name: ACRONYM_SPELLED.ehrVendor})).toBeVisible();

    await onboarding.continueButton.click();
    await onboarding.expectOnStep('manual-upload');

    // `advanceTo` saves before applying the transition, so the section fields are written.
    // What the pointer says is a separate matter — see draft-persistence.spec.ts.
    await expect
      .poll(async () => (await harness.storedDraft())?.facilityInfo)
      .toEqual({vendor: 'Epic', timeZone: 'America/New_York'});
  });

  test('unlocks the next rail entry only after the step is complete', async ({page, onboarding}) => {
    await expect(onboarding.stepButton(4)).toBeDisabled();

    await chooseOption(page, 'facilityEhrVendor', 'Epic');
    await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('manual-upload');

    await expect(onboarding.stepButton(4)).toBeEnabled();
    // Step 5 stays shut: FHIR needs its own completion, which includes a tested connection.
    await expect(onboarding.stepButton(5)).toBeDisabled();
  });
});
