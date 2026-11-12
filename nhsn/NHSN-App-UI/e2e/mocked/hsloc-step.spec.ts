import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {chooseOption} from '../support/fields';
import {censusComplete, draftAt, facilityInfoComplete, fhirComplete} from '../fixtures/drafts';

/**
 * Location Identification (HSLOC) — step 8. `validate.test.ts` already covers the pure rules, so
 * what is worth driving through the browser is the part those cannot see: where the rows come
 * from, which fields the capability flags leave out, the two-tab layout, and the handoff from a
 * manual-upload import.
 *
 * The rows are **not** read from the draft. They hydrate from `api.getHslocMappings()`, plus the
 * draft's *unresolved* import rows (blank `hslocCode`) — so a seeded draft that satisfies the
 * gate still arrives at an empty row, because `MockApiClient.getHslocMappings` returns nothing.
 *
 * The seeded vendor is Epic, whose `hslocSourceLabel` names the left-hand column
 * `Location.identifier.value`; Cerner's profile labels it differently.
 */

const LOCATION_VALUE_LABEL = 'Location.identifier.value';
const EMPTY_MESSAGE = 'Please fill all required fields before continuing.';

function locationValue(page: Page, index: number) {
  return page.locator(`#hsloc-location-value-${index}`);
}

function hslocDraft(overrides: Parameters<typeof draftAt>[1] = {}) {
  return draftAt('hsloc', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    ...overrides
  });
}

test.describe('location identification step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: hslocDraft(), path: '/onboarding/hsloc'});
  });

  test('starts on one empty mapping row, and refuses to continue on it', async ({page, onboarding}) => {
    // `minItems={1}`, so there is always a row to type into and never a zero-row state.
    await expect(locationValue(page, 0)).toBeVisible();
    await expect(locationValue(page, 1)).toHaveCount(0);
    await expect(page.getByRole('button', {name: 'Remove 1'})).toHaveCount(0);

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(EMPTY_MESSAGE);
    await onboarding.expectOnStep('hsloc');
  });

  test('collects two fields per row, not three, while the location-display capability is off', async ({page, onboarding}) => {
    // `hslocLocationDisplayUpdate` is false in the mock, and Normalization has nowhere to persist
    // the local display name, so the field is left out rather than collected and dropped. The
    // same flag relaxes `isRowComplete`, which is why two fields are enough to continue below.
    await expect(page.locator('#hsloc-your-code-0')).toHaveCount(0);
    await expect(onboarding.stepPanel).not.toContainText('Your (local) code');
    await expect(onboarding.stepPanel).toContainText(LOCATION_VALUE_LABEL);
  });

  test('carries over an imported row whose HSLOC code did not resolve', async ({page, harness, onboarding}) => {
    // The manual-upload importer deliberately lands a row with a blank `hslocCode` when the
    // spreadsheet's reference code matched nothing, so the facility can finish it here. Such a row
    // can never come back from `getHslocMappings` — Normalization's table is foreign-keyed to a
    // resolved code — so the draft is its only route onto this step.
    await harness.open({
      draft: hslocDraft({hsloc: {mappings: [{sourceCode: 'ICU-1', hslocCode: ''}]}}),
      path: '/onboarding/hsloc'
    });

    await expect(locationValue(page, 0)).toHaveValue('ICU-1');

    await chooseOption(page, 'hsloc-code-select-0', '1027-2 - Medical Ward (simulated)');
    await onboarding.continueButton.click();

    await onboarding.expectOnStep('encounter');
    expect((await harness.storedDraft())?.hsloc.mappings).toEqual([
      {sourceCode: 'ICU-1', hslocCode: '1027-2'}
    ]);
  });

  test('a facility whose mappings are already saved still arrives at an empty row', async ({page, harness}) => {
    // Characterisation of the mock, not of the app: `getHslocMappings` returns a hard-coded empty
    // list and the step hydrates from it, so mock mode cannot show an already-mapped facility.
    // Against a real BFF these rows would come back filled. It also means the gate and the panel
    // disagree here — the draft below is complete enough to have unlocked later steps.
    await harness.open({
      draft: hslocDraft({hsloc: {mappings: [{sourceCode: 'ICU-1', hslocCode: '1027-2'}]}}),
      path: '/onboarding/hsloc'
    });

    await expect(locationValue(page, 0)).toHaveValue('');
  });

  test('flags a location value that repeats another row', async ({page, onboarding}) => {
    await locationValue(page, 0).fill('ICU-1');
    await chooseOption(page, 'hsloc-code-select-0', '1027-2 - Medical Ward (simulated)');
    await page.getByRole('button', {name: '+ Add Mapping'}).click();
    await locationValue(page, 1).fill('icu-1');
    await chooseOption(page, 'hsloc-code-select-1', '1028-0 - Surgical Ward (simulated)');

    // Compared case-insensitively and trimmed, because the save path groups by local code the same
    // way and would otherwise keep one row and silently drop the other.
    const duplicate = 'This location code is already mapped in another row.';
    await expect(onboarding.stepPanel).toContainText(duplicate);
    expect((await onboarding.alertTexts()).filter((text) => text === duplicate)).toHaveLength(1);

    await onboarding.continueButton.click();

    // Two things about this assertion. The step-level message is announced on a `setTimeout`, so
    // it needs a retried expectation — `alertTexts()` is a one-shot read and would miss it. And
    // `AcronymText` spells the acronym out inside `textContent` ("HSLOCH S L O C"), so the
    // sentence is matched on the part either side of it.
    await expect(onboarding.stepPanel).toContainText(
      /Two or more rows map the same location code to different HSLOC/
    );
    await onboarding.expectOnStep('hsloc');
  });

  test('a Continue from the reference tab returns to the mapping tab to show the problem', async ({page, onboarding}) => {
    // The validation message and the flagged fields only render on the mapping tab, so leaving the
    // facility on the reference tab would reject the step with nothing visible to act on.
    await page.getByRole('tab', {name: /Reference/}).click();
    await expect(page.getByRole('textbox', {name: 'Search code,description'})).toBeVisible();

    await onboarding.continueButton.click();

    await expect(locationValue(page, 0)).toBeVisible();
    expect(await onboarding.alertTexts()).toContain(EMPTY_MESSAGE);
  });

  test('the reference tab filters the code list and counts what matched', async ({page, onboarding}) => {
    await page.getByRole('tab', {name: /Reference/}).click();

    const search = page.getByRole('textbox', {name: 'Search code,description'});
    // The count's accessible copy spells HSLOC out mid-word in `textContent`, so match the
    // numbers rather than the sentence.
    await expect(onboarding.stepPanel).toContainText(/2 of 2/);

    await search.fill('Surgical');
    await expect(onboarding.stepPanel).toContainText(/1 of 2/);
    await expect(onboarding.stepPanel).toContainText('Surgical Ward (simulated)');
    await expect(onboarding.stepPanel).not.toContainText('Medical Ward (simulated)');

    await search.fill('no such ward');
    await expect(onboarding.stepPanel).toContainText(/0 of 2/);
    await expect(onboarding.stepPanel).toContainText(/No matching HSLOC/);
    // The detail panel's "select a row" prompt only ever appears here, with no rows to select:
    // any non-empty list is auto-selected by the effect that keeps the selection inside the
    // filter, so the prompt is unreachable in the state it was written for.
    await expect(onboarding.stepPanel).toContainText(
      'Select a row to see which of your entered location codes map to it.'
    );
  });

  test('the reference detail panel names the local codes mapped to the selected code', async ({page, onboarding}) => {
    await locationValue(page, 0).fill('ICU-1');
    await chooseOption(page, 'hsloc-code-select-0', '1027-2 - Medical Ward (simulated)');

    await page.getByRole('tab', {name: /Reference/}).click();

    // Nothing has been clicked yet: the first code in the filtered list is selected for the
    // facility, so the panel opens on 1027-2 — which happens to be the one just mapped, and
    // already lists ICU-1 under it.
    await expect(page.locator('.nhsn-link__hsloc-detail-code')).toHaveText('1027-2');
    await expect(page.locator('.nhsn-link__summary-list')).toContainText('ICU-1');

    // The table rows are clickable `<tr>`s rather than buttons, which is itself worth pinning:
    // there is no keyboard route to a selection here.
    await page.locator('.nhsn-link__hsloc-row', {hasText: '1028-0'}).click();
    await expect(page.locator('.nhsn-link__hsloc-detail-code')).toHaveText('1028-0');
    await expect(onboarding.stepPanel).toContainText(/No location codes you've entered map to this HSLOC/);
  });

  test('advances with one complete row, and records the mapping in the draft', async ({page, harness, onboarding}) => {
    await locationValue(page, 0).fill('ICU-1');
    await chooseOption(page, 'hsloc-code-select-0', '1027-2 - Medical Ward (simulated)');

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('encounter');
    // `sourceDisplay` is left off entirely rather than written as an empty string — the field was
    // never collected, and `toMappings` turns a blank into `undefined`.
    expect((await harness.storedDraft())?.hsloc.mappings).toEqual([
      {sourceCode: 'ICU-1', hslocCode: '1027-2'}
    ]);
  });
});
