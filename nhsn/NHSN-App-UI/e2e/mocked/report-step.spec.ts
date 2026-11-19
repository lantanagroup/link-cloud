import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {typeDate} from '../support/fields';
import {
  censusComplete,
  draftAt,
  encounterComplete,
  facilityInfoComplete,
  fhirComplete,
  hslocComplete
} from '../fixtures/drafts';

/**
 * Generate Test Report — step 10, and the only step that completes by calling the API rather than
 * by filling fields: `isReportComplete` reads `lastRequestedReportId`, so until a request has been
 * accepted the step is unfinished however complete the form looks.
 *
 * Three controls here are each a different kind of widget: Measures is the hand-rolled
 * `ChipMultiSelect` (a sibling listbox, no Kendo popup container), the dates are Kendo masked
 * date pickers (see `typeDate`), and the patient ids are a `RepeatableList` inside a tab set.
 */

const MEASURE = 'Glycemic Control (simulated)';
const SECOND_MEASURE = 'Acute Care Hospital Monthly (simulated)';
const INCOMPLETE = 'Please complete all fields before continuing.';

function reportDraft(overrides: Parameters<typeof draftAt>[1] = {}) {
  return draftAt('report', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    hsloc: hslocComplete(),
    encounter: encounterComplete(),
    ...overrides
  });
}

async function chooseMeasure(page: Page, name: string): Promise<void> {
  await page.locator('#reportMeasures').click();
  await page.getByRole('option', {name}).click();
}

async function addPatientId(page: Page, index: number, id: string): Promise<void> {
  await page.getByRole('button', {name: '+ Add Patient ID'}).click();
  await page.locator(`#reportPatientId-${index}`).fill(id);
}

async function fillValidReport(page: Page): Promise<void> {
  await chooseMeasure(page, MEASURE);
  await typeDate(page, 'reportStartDate', '10-01-2026');
  await typeDate(page, 'reportEndDate', '10-31-2026');
  await addPatientId(page, 0, 'patient-001');
}

test.describe('generate test report step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: reportDraft(), path: '/onboarding/report'});
  });

  test('refuses to generate until every field is filled in', async ({page, onboarding}) => {
    await page.getByRole('button', {name: 'Generate Report'}).click();

    await expect(onboarding.stepPanel).toContainText(INCOMPLETE);
    const alerts = await onboarding.alertTexts();
    expect(alerts).toContain('Select at least one measure.');
    expect(alerts).toContain('A report start date is required.');
    expect(alerts).toContain('A report end date is required.');
    await onboarding.expectOnStep('report');
  });

  test('picks measures from a filtered list, and keeps their names rather than their ids', async ({page, harness}) => {
    await page.locator('#reportMeasures').click();
    await expect(page.getByRole('option', {name: MEASURE})).toBeVisible();

    // The picker filters on typed text and says so when nothing matches.
    await page.locator('#reportMeasures').fill('acute');
    await expect(page.getByRole('option', {name: SECOND_MEASURE})).toBeVisible();
    await expect(page.getByRole('option', {name: MEASURE})).toHaveCount(0);
    await page.locator('#reportMeasures').fill('no such measure');
    await expect(page.getByRole('listbox')).toContainText('No matching measures.');

    await page.locator('#reportMeasures').fill('');
    await page.getByRole('option', {name: MEASURE}).click();

    // A chip per selection, each with its own remove control, and the list is additive.
    await expect(page.getByRole('button', {name: `Remove ${MEASURE}`})).toBeVisible();
    await page.getByRole('option', {name: SECOND_MEASURE}).click();
    await expect(page.getByRole('list', {name: 'Selected measures'}).getByRole('listitem')).toHaveCount(2);
    // What reaches the draft is asserted by the request test below rather than here: the draft is
    // persisted on save, so reading storage mid-step would only show the seeded state.
  });

  test('drops a saved measure the facility is no longer offered', async ({page, harness, onboarding}) => {
    // A draft saved against an earlier `getAvailableMeasures` can name a measure that has since
    // gone from the facility's plan. `ChipMultiSelect` would render it as its own chip, so the
    // step filters it out of the selection instead — and the step is then incomplete again.
    await harness.open({
      draft: reportDraft({report: {measures: ['Retired Measure (simulated)']}}),
      path: '/onboarding/report'
    });

    await expect(page.getByRole('button', {name: /^Remove Retired Measure/})).toHaveCount(0);
    await page.getByRole('button', {name: 'Generate Report'}).click();
    expect(await onboarding.alertTexts()).toContain('Select at least one measure.');
  });

  test('rejects an end date before the start date', async ({page, onboarding}) => {
    await chooseMeasure(page, MEASURE);
    await typeDate(page, 'reportStartDate', '10-31-2026');
    await typeDate(page, 'reportEndDate', '10-01-2026');
    await addPatientId(page, 0, 'patient-001');

    await page.getByRole('button', {name: 'Generate Report'}).click();

    expect(await onboarding.alertTexts()).toContain(
      'The report end date cannot precede the start date.'
    );
    await onboarding.expectOnStep('report');
  });

  test('treats a blank patient row as unfinished, not as nothing entered', async ({page, onboarding}) => {
    await chooseMeasure(page, MEASURE);
    await typeDate(page, 'reportStartDate', '10-01-2026');
    await typeDate(page, 'reportEndDate', '10-31-2026');
    await addPatientId(page, 0, 'patient-001');
    await page.getByRole('button', {name: '+ Add Patient ID'}).click();

    await page.getByRole('button', {name: 'Generate Report'}).click();

    // The row says what is wrong with it, and the step-level banner carries the rest. The more
    // specific "Complete or remove blank FHIR Patient ID fields before continuing." is computed
    // but deliberately not shown — `fieldError` suppresses the patient-ids message unless a
    // duplicate is the only problem, so that one row cannot collect two messages about itself.
    await expect(onboarding.stepPanel).toContainText('FHIR Patient ID is required.');
    await expect(onboarding.stepPanel).toContainText(INCOMPLETE);
    await expect(onboarding.stepPanel).not.toContainText('Complete or remove blank FHIR Patient ID');
    await onboarding.expectOnStep('report');
  });

  test('a repeated patient id is reported on the row, without the step-level banner', async ({page, onboarding}) => {
    await chooseMeasure(page, MEASURE);
    await typeDate(page, 'reportStartDate', '10-01-2026');
    await typeDate(page, 'reportEndDate', '10-31-2026');
    await addPatientId(page, 0, 'patient-001');
    await addPatientId(page, 1, 'patient-001');

    await page.getByRole('button', {name: 'Generate Report'}).click();

    // Everything *is* filled in, so the generic "complete all fields" banner would be wrong here.
    // The step suppresses it when a duplicate id is the only problem.
    await expect(onboarding.stepPanel).toContainText('This FHIR Patient ID has already been added.');
    await expect(onboarding.stepPanel).not.toContainText(INCOMPLETE);
    await onboarding.expectOnStep('report');
  });

  test('offers only manual entry and CSV upload while the census capability is off', async ({page}) => {
    // The two census-pull tabs are gated on `patientListWithNames`/`sftpFileListing`, both false in
    // the mock, so this step cannot pull a census here at all.
    const tabs = page.getByRole('tablist', {name: 'FHIR Patient ID(s)'});
    await expect(tabs.getByRole('tab', {name: 'Manual Entry'})).toBeVisible();
    await expect(tabs.getByRole('tab', {name: 'Upload CSV'})).toBeVisible();
    await expect(tabs.getByRole('tab', {name: 'From Previous Census Pull'})).toHaveCount(0);
    await expect(tabs.getByRole('tab', {name: 'New Census Pull'})).toHaveCount(0);
  });

  test('stops at ten patient ids', async ({page, harness, onboarding}) => {
    // Seeded rather than clicked ten times — and seeded through the harness, because a bare
    // `reload()` would drop the harness back to BFF mode and unmount the wizard.
    const ids = Array.from({length: 10}, (_value, index) => `patient-${String(index + 1).padStart(3, '0')}`);
    await harness.open({draft: reportDraft({report: {patientIds: ids}}), path: '/onboarding/report'});

    const add = page.getByRole('button', {name: '+ Add Patient ID'});
    await expect(page.locator('[id^="reportPatientId-"]')).toHaveCount(10);

    // The cap holds — clicking adds nothing — but it holds in the handler only. At the ceiling the
    // button is given `nhsn-link__button--disabled` and nothing else: no `disabled` attribute and
    // no `aria-disabled`, so it looks inert while still being focusable and announced as an
    // available action. A keyboard or screen-reader user is invited to press a button that does
    // nothing. Asserted as it is, not as it should be.
    await expect(add).toBeEnabled();
    await expect(add).toHaveClass(/nhsn-link__button--disabled/);
    await add.click();
    await expect(page.locator('[id^="reportPatientId-"]')).toHaveCount(10);
    await expect(onboarding.stepPanel).toContainText('Maximum of 10 FHIR Patient IDs reached.');
  });

  test('requests the report, and only then counts as complete', async ({page, harness, onboarding}) => {
    await fillValidReport(page);

    await page.getByRole('button', {name: 'Generate Report'}).click();

    // The request is accepted synchronously and the report is generated behind it; nothing here
    // waits for a finished report.
    await onboarding.expectOnStep('report-results');
    const stored = await harness.storedDraft();
    expect(stored?.report.lastRequestedReportId).toBe('SIMULATED-REPORT-0001');
    expect(stored?.report.startDate).toBe('2026-10-01');
    expect(stored?.report.endDate).toBe('2026-10-31');
    expect(stored?.report.patientIds).toEqual(['patient-001']);
    // Report stores only the resolved dQM, so the names behind the request are kept here for the
    // Report Results and Details views to show.
    expect(stored?.reportResults.requestedMeasuresByReportId).toEqual({
      'SIMULATED-REPORT-0001': [MEASURE]
    });
  });
});
