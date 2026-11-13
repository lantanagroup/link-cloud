import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {CENSUS_LIST_KEYS} from '../../src/core/onboarding/steps/census/validate';
import {draftAt, facilityInfoComplete, fhirComplete} from '../fixtures/drafts';

/**
 * Patients of Interest Configuration — step 6, and the first step whose shape depends on the
 * vendor profile rather than only on the draft. The seeded vendor is Epic, so
 * `vendorProfile.censusAcquisition` is `PatientList` and the panel asks for six FHIR List ids;
 * seed Cerner and the same step renders the sFTP branch instead.
 *
 * `capabilities.patientListWithNames` is false in the mock, which removes the whole live-query
 * half of the step: no per-list validation, no results preview, and — because `validationLive`
 * follows the same flag — no accuracy acknowledgement in the Continue gate.
 */

/** `census-list-<key>`, the id `CensusStep` gives each TextField. */
function listField(page: Page, key: string) {
  return page.locator(`#census-list-${key}`);
}

/**
 * Kendo's NumericTextBox reports through its own onChange, and `CensusStep` recomputes the
 * field error on blur, so a filled value has to be committed before Continue is clicked.
 */
async function setFrequency(page: Page, hours?: number, minutes?: number): Promise<void> {
  if (hours !== undefined) {
    await page.locator('#census-frequency-hours').fill(String(hours));
    await page.keyboard.press('Tab');
  }
  if (minutes !== undefined) {
    await page.locator('#census-frequency-minutes').fill(String(minutes));
    await page.keyboard.press('Tab');
  }
}

async function fillEveryListId(page: Page): Promise<void> {
  for (const [index, key] of CENSUS_LIST_KEYS.entries()) {
    await listField(page, key).fill(`epic-list-${index + 1}`);
  }
}

test.describe('patients of interest step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({
      draft: draftAt('census', {facilityInfo: facilityInfoComplete(), fhir: fhirComplete()}),
      path: '/onboarding/census'
    });
  });

  test('asks for all six Epic patient lists, and requires every one', async ({page, onboarding}) => {
    await expect(onboarding.stepPanel).toContainText('Epic Patient Lists');
    for (const key of CENSUS_LIST_KEYS) {
      await expect(listField(page, key)).toBeVisible();
    }

    await onboarding.continueButton.click();

    const alerts = await onboarding.alertTexts();
    // One message per list, plus the summary. The six are the point: validating only the first
    // empty field would let a half-filled panel look like one bad row.
    expect(alerts.filter((text) => text.endsWith('List Id is required.'))).toHaveLength(6);
    expect(alerts).toContain('Admit - Within 24 Hours List Id is required.');
    expect(alerts).toContain('Please complete all required fields before continuing.');
    await onboarding.expectOnStep('census');
  });

  test('rejects a list id that repeats another, flagging the field being edited', async ({page, onboarding}) => {
    await fillEveryListId(page);
    // The duplicate lands on the field being typed in, not on whichever sits later in the list
    // — `findDuplicatePatientListIdKeys` keeps the edited key out of the one clean slot.
    await listField(page, 'discharge-gt-48').fill('epic-list-1');

    await expect(onboarding.stepPanel).toContainText(
      'Duplicate IDs are not allowed. Please ensure all entered IDs are unique.'
    );
    expect(
      (await onboarding.alertTexts()).filter((text) => text.startsWith('Duplicate IDs'))
    ).toHaveLength(1);

    await onboarding.continueButton.click();
    await onboarding.expectOnStep('census');
  });

  test('requires a frequency of at least five minutes, in two layers', async ({page, onboarding}) => {
    await fillEveryListId(page);

    // Minutes on its own carries the 5-minute floor, because blank hours counts as zero.
    await setFrequency(page, undefined, 2);
    await expect(onboarding.stepPanel).toContainText('Enter a whole number of minutes between 5 and 59.');

    // Give hours a value and the minutes field reverts to its plain 0-59 range — the floor is
    // then enforced on the total instead, which is the second layer.
    await setFrequency(page, 0, 1);
    await onboarding.continueButton.click();
    expect(await onboarding.alertTexts()).toContain(
      'Census Acquisition Frequency must be at least 5 minutes.'
    );
    await onboarding.expectOnStep('census');
  });

  test('the 24-hour ceiling cannot be reached from the two inputs', async ({page, harness, onboarding}) => {
    // Characterisation, not an endorsement. Hours is held to 0-23 and minutes to 0-59, so the
    // largest total the panel can express is 23h59m = 1,439 minutes — one minute under the
    // 1,440 that `frequencyTooLong` exists to catch. The copy is live, but only a stored value
    // can trigger it (a draft import, or a BFF serving a longer duration), never typing.
    await fillEveryListId(page);
    await setFrequency(page, 24, 0);
    await expect(onboarding.stepPanel).toContainText('Enter a whole number of hours between 0 and 23.');
    await expect(onboarding.stepPanel).not.toContainText('must be no more than 24 hours');

    // An out-of-range entry deliberately leaves the last valid persisted duration alone rather
    // than clamping behind the user's back (`updateFrequency`), so nothing was written here.
    expect((await harness.storedDraft())?.census.acquisitionFrequency).toBeUndefined();

    await harness.open({
      draft: draftAt('census', {
        facilityInfo: facilityInfoComplete(),
        fhir: fhirComplete(),
        census: {acquisitionFrequency: 'PT25H0M'}
      }),
      path: '/onboarding/census'
    });
    await onboarding.continueButton.click();
    expect(await onboarding.alertTexts()).toContain(
      'Census Acquisition Frequency must be no more than 24 hours.'
    );
  });

  test('offers no validation or acknowledgement while the patient-list capability is off', async ({page, onboarding}) => {
    // Every capability is false in the mock, and `patientListsLive` gates the live half of the
    // step. Asserting the absence keeps the capability wiring honest: turn the flag on and
    // these controls — and a fourth Continue gate — appear.
    //
    // Each absence is asserted on the copy as well as on the role, because an absence proved
    // only by a role query passes just as happily when the query itself is wrong.
    await expect(onboarding.stepPanel).not.toContainText('Validate Census Results');
    await expect(onboarding.stepPanel).not.toContainText('Export Results');
    await expect(onboarding.stepPanel).not.toContainText(
      'I acknowledge that the patients of interest results are accurate.'
    );
    await expect(page.getByRole('button', {name: 'Validate Census Results'})).toHaveCount(0);
    await expect(page.getByRole('button', {name: /^View results for /})).toHaveCount(0);
    await expect(page.getByRole('checkbox', {name: /patients of interest results are accurate/})).toHaveCount(0);
    await expect(onboarding.stepPanel).not.toContainText('List Results Preview');
  });

  test('advances once every list and the frequency are set, and records them in the draft', async ({page, harness, onboarding}) => {
    await fillEveryListId(page);
    await setFrequency(page, 1, 0);

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('location-org');
    const stored = await harness.storedDraft();
    // Durations persist as the ISO-8601 form `buildHoursMinutesDuration` writes, not as the
    // hours/minutes pair the panel shows.
    expect(stored?.census.acquisitionFrequency).toBe('PT1H0M');
    expect(stored?.census.patientListIds).toMatchObject({
      'admit-lt-24': 'epic-list-1',
      'discharge-gt-48': 'epic-list-6'
    });
  });
});
