import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {
  censusComplete,
  draftAt,
  encounterComplete,
  facilityInfoComplete,
  fhirComplete,
  hslocComplete,
  reportComplete,
  reportResultsAcknowledged
} from '../fixtures/drafts';

/**
 * MRN Identifier Intake — step 12, a questionnaire rather than a configuration form, and the
 * last step with a gate. It is only reachable here from a seeded draft: step 11's Continue cannot
 * be satisfied in mock mode (see `report-results-step.spec.ts`).
 *
 * Its Yes/No questions are **tabs**, not radios — `YesNoTabsField` renders the shared `Tabs`
 * widget, so each question is a tablist named after the whole question and each answer is a
 * `role="tab"`.
 */

function question(page: Page, text: RegExp) {
  return page.getByRole('tablist', {name: text});
}

async function answer(page: Page, text: RegExp, choice: 'Yes' | 'No'): Promise<void> {
  await question(page, text).getByRole('tab', {name: choice, exact: true}).click();
}

const MULTIPLE_MRN = /multiple MRN-like patient identifiers/;
const VARIES_BY_FACILITY = /vary by facility\/campus\/setting/;
const CHANGES_OVER_TIME = /change over time for the same patient/;

function mrnDraft() {
  return draftAt('mrn-intake', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    hsloc: hslocComplete(),
    encounter: encounterComplete(),
    report: reportComplete(),
    reportResults: reportResultsAcknowledged()
  });
}

async function addRule(page: Page, value: string): Promise<void> {
  await page.getByRole('button', {name: '+ Add Rule'}).click();
  // A new rule arrives as `identifier.system` / `Equals`, which needs a value.
  await page.locator('#mrn-rule-value-0').fill(value);
}

test.describe('MRN identifier intake step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: mrnDraft(), path: '/onboarding/mrn-intake'});
  });

  test('asks three questions as tab pairs, and answers none of them by default', async ({page, onboarding}) => {
    for (const text of [MULTIPLE_MRN, VARIES_BY_FACILITY, CHANGES_OVER_TIME]) {
      const tabs = question(page, text);
      await expect(tabs.getByRole('tab', {name: 'Yes', exact: true})).toHaveAttribute('aria-selected', 'false');
      await expect(tabs.getByRole('tab', {name: 'No', exact: true})).toHaveAttribute('aria-selected', 'false');
    }

    await page.getByRole('button', {name: 'Complete Enrollment'}).click();

    const alerts = await onboarding.alertTexts();
    // One "Please select Yes or No." per unanswered question, plus the rule.
    expect(alerts.filter((text) => text === 'Please select Yes or No.')).toHaveLength(3);
    expect(alerts).toContain('Please add at least one rule.');
    await onboarding.expectOnStep('mrn-intake');
  });

  test('still demands a rule from a facility that says it has only one MRN', async ({page, onboarding}) => {
    // Characterisation of a real inconsistency. Every other section is gated on its own answer —
    // the identifier types, the user-facing name and the search question all appear only when
    // multiple MRNs are declared — but `validateMrnIntake` requires `rules.length > 0`
    // unconditionally. The section's own copy says rules identify the usable MRN "when multiple
    // identifiers are present", so a facility that has just said it has one is asked to invent a
    // disambiguation rule anyway.
    await answer(page, MULTIPLE_MRN, 'No');
    await answer(page, VARIES_BY_FACILITY, 'No');
    await answer(page, CHANGES_OVER_TIME, 'No');

    await page.getByRole('button', {name: 'Complete Enrollment'}).click();

    // The rule is the *only* thing it is still waiting for — every question has an answer.
    expect(await onboarding.alertTexts()).toEqual([
      'Please add at least one rule.',
      'Please complete all fields before continuing.'
    ]);
    await onboarding.expectOnStep('mrn-intake');
  });

  test('answering yes to multiple MRNs opens a section with questions of its own', async ({page, onboarding}) => {
    await expect(onboarding.stepPanel).not.toContainText('USER-FACING MRN IN THE EHR');

    await answer(page, MULTIPLE_MRN, 'Yes');

    // The follow-up section and its own Yes/No questions only exist in this branch.
    await expect(onboarding.stepPanel).toContainText('USER-FACING MRN IN THE EHR');
    await expect(onboarding.stepPanel).toContainText(
      'Select all that apply (these will not be used for displaying your data in NHSN reports):'
    );

    await answer(page, VARIES_BY_FACILITY, 'No');
    await answer(page, CHANGES_OVER_TIME, 'No');
    await addRule(page, 'http://hospital.org/fhir/identifier/mrn');
    await page.getByRole('button', {name: 'Complete Enrollment'}).click();

    // Declaring multiple MRNs without saying which they are leaves the branch incomplete.
    expect(await onboarding.alertTexts()).toContain('Please select at least one option.');
    await onboarding.expectOnStep('mrn-intake');
  });

  test('requires a value on a rule whose operator compares one', async ({page, onboarding}) => {
    await answer(page, MULTIPLE_MRN, 'No');
    await answer(page, VARIES_BY_FACILITY, 'No');
    await answer(page, CHANGES_OVER_TIME, 'No');
    await page.getByRole('button', {name: '+ Add Rule'}).click();

    await page.getByRole('button', {name: 'Complete Enrollment'}).click();

    // The default rule is `identifier.system` / `Equals`, so it has something to compare against.
    // "Must be present" and "Must be absent" need no value, and the field is not shown for them.
    expect(await onboarding.alertTexts()).toContain('Please fill the required field.');
    await onboarding.expectOnStep('mrn-intake');
  });

  test('completes the enrollment with three answers and one rule', async ({page, onboarding}) => {
    await answer(page, MULTIPLE_MRN, 'No');
    await answer(page, VARIES_BY_FACILITY, 'No');
    await answer(page, CHANGES_OVER_TIME, 'No');
    await addRule(page, 'http://hospital.org/fhir/identifier/mrn');

    await page.getByRole('button', {name: 'Complete Enrollment'}).click();

    await onboarding.expectOnStep('complete');
  });
});
