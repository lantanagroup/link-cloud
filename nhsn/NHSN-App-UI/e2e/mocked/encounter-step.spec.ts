import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {
  censusComplete,
  draftAt,
  facilityInfoComplete,
  fhirComplete,
  hslocComplete
} from '../fixtures/drafts';

/**
 * Encounter Mapping — step 9, and the clearest case in the flow of the rail and the step
 * disagreeing. `flow.ts` declares `isComplete: () => true` with the comment "Nothing here is
 * mandatory, matching the POC", while the step refuses to continue without a code system. The
 * first test pins both halves of that.
 *
 * The step builds its groups from the draft (unlike HSLOC, which hydrates from the API), and the
 * CPT/SNOMED picker is hand-rolled rather than Kendo — a plain `role="combobox"` over a sibling
 * listbox, so none of the `.k-animation-container` handling applies.
 */

const REQUIRED_CODE_SYSTEM = 'At least one Encounter.type Code System is required.';
const INCOMPLETE = "Please complete each mapping row's fields, or remove the row, before continuing.";
const CODE_SYSTEM = 'http://mysite.org/fhir/encounter-type';

/**
 * Both of these need `exact`. `getByRole`'s name option matches a *substring*, case-insensitively,
 * so "Encounter.type code" also matches the group's "Encounter.type Code System" input — which
 * sits first in the DOM, so an inexact locator silently types the local code into the code system
 * and the row stays empty. The step then rejects a form that looks filled in on screen.
 */
function codeSystemInput(page: Page, index = 0) {
  return page.getByRole('textbox', {name: 'Encounter.type Code System', exact: true}).nth(index);
}

function localCode(page: Page, index = 0) {
  return page.getByRole('textbox', {name: 'Encounter.type code', exact: true}).nth(index);
}

function targetCode(page: Page, index = 0) {
  return page.getByRole('combobox', {name: 'CPT or SNOMED code'}).nth(index);
}

function encounterDraft() {
  return draftAt('encounter', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    hsloc: hslocComplete()
  });
}

test.describe('encounter mapping step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: encounterDraft(), path: '/onboarding/encounter'});
  });

  test('requires a code system, although the flow declares the step optional', async ({onboarding}) => {
    // `flow.ts` would let the rail treat this step as satisfied by nothing at all. Continue is
    // the only way forward regardless — unlock happens through `advanceTo`, never from the rail —
    // so the dead `isComplete` costs no navigation, which is exactly why it has gone unnoticed.
    await expect(onboarding.stepButton(10)).toBeDisabled();

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(REQUIRED_CODE_SYSTEM);
    await onboarding.expectOnStep('encounter');
    await expect(onboarding.stepButton(10)).toBeDisabled();
  });

  test('accepts a code system with no mappings under it at all', async ({page, harness, onboarding}) => {
    // Only rows that exist are checked, so a code system on its own is a complete answer: the
    // facility is saying which system its local codes come from without mapping any yet.
    await codeSystemInput(page).fill(CODE_SYSTEM);

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('report');
    const stored = await harness.storedDraft();
    expect(stored?.encounter.codeSystems).toEqual([CODE_SYSTEM]);
    expect(stored?.encounter.mappings).toEqual([]);
  });

  test('reports a blank second code system as a mapping-row problem', async ({page, onboarding}) => {
    await codeSystemInput(page).fill(CODE_SYSTEM);
    await page.getByRole('button', {name: '+ Add Code System'}).click();

    await onboarding.continueButton.click();

    // The field-level hint is right ("Encounter.type Code System is required."), but the step-level
    // message is about mapping rows, because `validationMessageFor` routes a missing code system to
    // `messages.incomplete`. The dedicated `messages.missingCodeSystem` string — "Fill in each
    // Encounter.type Code System, or remove it, before continuing." — is read by no code.
    expect(await onboarding.alertTexts()).toContain(INCOMPLETE);
    await expect(onboarding.stepPanel).toContainText('Encounter.type Code System is required.');
    await expect(onboarding.stepPanel).not.toContainText('Fill in each Encounter.type Code System');
    await onboarding.expectOnStep('encounter');
  });

  test('refuses two groups sharing one code system', async ({page, onboarding}) => {
    await codeSystemInput(page).fill(CODE_SYSTEM);
    await page.getByRole('button', {name: '+ Add Code System'}).click();
    await codeSystemInput(page, 1).fill(CODE_SYSTEM);

    await onboarding.continueButton.click();

    // Not cosmetic: the draft round-trips code systems as a flat, system-keyed list, so two groups
    // with one value collapse into one on the next Continue/Back cycle and a group's rows vanish.
    expect(await onboarding.alertTexts()).toContain(
      'Two or more Encounter.type Code Systems are the same (or both blank). Fix or remove one of them before continuing.'
    );
    await expect(onboarding.stepPanel).toContainText(
      'This code system is already used by another group above'
    );
    await onboarding.expectOnStep('encounter');
  });

  test('a mapping row must have both sides filled, or be removed', async ({page, onboarding}) => {
    await codeSystemInput(page).fill(CODE_SYSTEM);
    await page.getByRole('button', {name: '+ Add Mapping'}).click();
    await localCode(page).fill('LOCAL-1');

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(INCOMPLETE);
    await expect(onboarding.stepPanel).toContainText('This mapping is missing a CPT/SNOMED code.');
    await onboarding.expectOnStep('encounter');

    // Removing the half-filled row is the other way past the gate, and leaves the code system.
    await page.getByRole('button', {name: /^Remove mapping/}).click();
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('report');
  });

  test('the CPT/SNOMED picker filters as it is typed, and fills the row from the chosen option', async ({page, harness, onboarding}) => {
    await codeSystemInput(page).fill(CODE_SYSTEM);
    await page.getByRole('button', {name: '+ Add Mapping'}).click();
    await localCode(page).fill('LOCAL-1');

    // Hand-rolled combobox: the options are a sibling listbox, filtered by `getEncounterCodes`
    // against system, code, display and category, and committed on click.
    await targetCode(page).click();
    await targetCode(page).fill('forequarter');
    const option = page.getByRole('option', {name: /23900/});
    await expect(option).toBeVisible();
    await option.click();

    await expect(targetCode(page)).toHaveValue(/23900/);

    await onboarding.continueButton.click();

    await onboarding.expectOnStep('report');
    const stored = await harness.storedDraft();
    // Each mapping names the code system it sits under, its local code, and the reference code as
    // the encoded `encounterType` pair the BFF expects.
    expect(stored?.encounter.codeSystems).toEqual([CODE_SYSTEM]);
    expect(stored?.encounter.mappings).toHaveLength(1);
    expect(stored?.encounter.mappings?.[0]).toMatchObject({system: CODE_SYSTEM, code: 'LOCAL-1'});
    expect(stored?.encounter.mappings?.[0].encounterType).toContain('23900');
  });

  test('the reference tab filters by text and by code system', async ({page, onboarding}) => {
    await page.getByRole('tab', {name: /Reference/}).click();

    const search = page.locator('#encounterSearch');
    await expect(onboarding.stepPanel).toContainText(/10 of 10 codes/);

    await search.fill('amputation');
    await expect(onboarding.stepPanel).toContainText('Interthoracoscapular amputation (forequarter)');
    await expect(onboarding.stepPanel).not.toContainText('34830');

    await search.fill('');
    await expect(onboarding.stepPanel).toContainText(/10 of 10 codes/);
    await search.fill('no such procedure');
    await expect(onboarding.stepPanel).toContainText('No matching codes.');
  });
});
