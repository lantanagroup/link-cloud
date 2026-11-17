import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {censusComplete, draftAt, facilityInfoComplete, fhirComplete} from '../fixtures/drafts';

/**
 * Organization Identification — step 7. `flow.ts` gives it `isComplete: () => true`, so the step
 * is genuinely optional and the rail unlocks HSLOC whether or not anything is configured. What it
 * does enforce, on Continue, is that no *half-finished* row is left behind.
 *
 * Which resolution methods the step offers comes from the vendor profile, not from the draft:
 * Epic declares `['location-identifier', 'custom-fhir-path']`, Cerner the other three. The seeded
 * vendor is Epic throughout, so Managing Organization and Location Type are out of scope here.
 */

/** The tab row is the `Tabs` adapter — `role="tab"` in a tablist named after the field. */
function methodTab(page: Page, name: string) {
  return page.getByRole('tablist', {name: 'Resolution Method'}).getByRole('tab', {name});
}

function addRow(page: Page) {
  return page.getByRole('button', {name: /Add Location Identifier/});
}

async function fillRow(page: Page, index: number, system: string, code: string): Promise<void> {
  if (system) {
    await page.locator(`#location-identifier-system-${index}`).fill(system);
  }
  if (code) {
    await page.locator(`#location-identifier-code-${index}`).fill(code);
  }
}

const INCOMPLETE_ROWS =
  'Every row needs both fields filled in - remove or finish any partial rows before continuing.';

test.describe('organization identification step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({
      draft: draftAt('location-org', {
        facilityInfo: facilityInfoComplete(),
        fhir: fhirComplete(),
        census: censusComplete()
      }),
      path: '/onboarding/location-org'
    });
  });

  test('offers only the resolution methods the vendor profile declares', async ({page}) => {
    await expect(methodTab(page, 'Location Identifier')).toBeVisible();
    await expect(methodTab(page, 'Custom FHIRPath')).toBeVisible();
    // Managing Organization and Location Type belong to Cerner's profile. Seeding Cerner swaps
    // the set — the step reads `vendorProfile.locationMethods` and nothing else.
    await expect(methodTab(page, 'Managing Organization')).toHaveCount(0);
    await expect(methodTab(page, 'Location Type')).toHaveCount(0);
    // The first declared method is selected for the facility rather than left unchosen.
    await expect(methodTab(page, 'Location Identifier')).toHaveAttribute('aria-selected', 'true');
  });

  test('is optional: Continue with nothing configured reaches HSLOC', async ({page, onboarding}) => {
    // An empty list renders as nothing at all — no rows, no column headings, and no empty-state
    // copy: `locationOrg.noneAdded` exists in the bundle but is read by no component.
    await expect(page.locator('#location-identifier-system-0')).toHaveCount(0);
    await expect(onboarding.stepPanel).not.toContainText('None added yet.');

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('hsloc');
  });

  test('an added row must be finished or removed, even while still blank', async ({page, onboarding}) => {
    await addRow(page).click();

    // A row that exists but holds nothing is as ambiguous as a half-filled one, so the blank row
    // itself blocks — there is no "untouched rows do not count" exemption.
    await onboarding.continueButton.click();
    expect(await onboarding.alertTexts()).toContain(INCOMPLETE_ROWS);
    await onboarding.expectOnStep('location-org');

    // Removing it is the other way out of the gate, and `minItems={0}` allows going back to none.
    await page.getByRole('button', {name: 'Remove 1'}).click();
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('hsloc');
  });

  test('rejects a row with only one of its two fields', async ({page, onboarding}) => {
    await addRow(page).click();
    await fillRow(page, 0, 'urn:oid:1.2.3', '');

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(INCOMPLETE_ROWS);
    await onboarding.expectOnStep('location-org');
  });

  test('flags a repeated system and code pair as it is typed', async ({page, onboarding}) => {
    await addRow(page).click();
    await fillRow(page, 0, 'urn:oid:1.2.3', 'ABC');
    await addRow(page).click();
    await fillRow(page, 1, 'urn:oid:1.2.3', 'ABC');

    // Live, without Continue: a repeated pair configures the same thing twice and is wrong the
    // moment it is typed. The error lands on the row being edited, so only one row carries it —
    // comparison is trimmed and case-insensitive (`pairKey`), not literal.
    const duplicate = 'This Location Identifier System and Code combination is already used in another row.';
    await expect(onboarding.stepPanel).toContainText(duplicate);
    expect((await onboarding.alertTexts()).filter((text) => text === duplicate)).toHaveLength(1);

    await onboarding.continueButton.click();
    expect(await onboarding.alertTexts()).toContain(
      'Two or more rows have the same Location Identifier System and Code. Each combination must be unique.'
    );
    await onboarding.expectOnStep('location-org');
  });

  test('switching method takes the unfinished row out of the gate, and discards it', async ({page, harness, onboarding}) => {
    await addRow(page).click();
    await fillRow(page, 0, 'urn:oid:1.2.3', '');
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('location-org');

    // Only the active method's rows are checked, so switching tab clears the blockage. The row is
    // not merely ignored, though: `discardInactiveTabs` empties every inactive method the vendor
    // offers before advancing, because the BFF only ever saves the active one and anything left in
    // the others would come back on the next visit as an abandoned half-row.
    await methodTab(page, 'Custom FHIRPath').click();
    await onboarding.continueButton.click();

    await onboarding.expectOnStep('hsloc');
    expect((await harness.storedDraft())?.locationOrg.locationIdentifiers).toEqual([]);
  });

  test('accepts any Custom FHIRPath expression, leaving the verdict to Data Acquisition', async ({page, harness, onboarding}) => {
    await methodTab(page, 'Custom FHIRPath').click();
    // Free-form by design: the step compiles nothing, and `locationOrg.errors.invalidFhirPath`
    // exists in the copy but is read by no code at all. If client-side checking is ever added,
    // this test is where it should start failing.
    await page.locator('#custom-fhir-path').fill('this is not FHIRPath(((');

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('hsloc');
    expect((await harness.storedDraft())?.locationOrg.customFhirPath).toBe('this is not FHIRPath(((');
  });

  test('advances with a complete row, and records it in the draft', async ({page, harness, onboarding}) => {
    await addRow(page).click();
    await fillRow(page, 0, 'urn:oid:2.16.840.1.113883.3.42', 'FAC-001');

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('hsloc');
    const stored = await harness.storedDraft();
    expect(stored?.locationOrg.locationIdentifiers).toEqual([
      {system: 'urn:oid:2.16.840.1.113883.3.42', code: 'FAC-001'}
    ]);
    expect(stored?.locationOrg.method).toBe('location-identifier');
  });
});
