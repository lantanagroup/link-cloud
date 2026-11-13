import {expect, test} from '../support/test';
import type {Page} from '@playwright/test';
import {chooseOption, typeDate} from '../support/fields';
import {CENSUS_LIST_KEYS} from '../../src/core/onboarding/steps/census/validate';
import {
  censusComplete,
  draftAt,
  encounterComplete,
  facilityInfoComplete,
  fhirComplete,
  hslocComplete,
  MOCK_REPORT_ID,
  reportComplete,
  reportResultsAcknowledged
} from '../fixtures/drafts';

/**
 * One continuous session that walks a facility through onboarding the way a person would:
 * welcome → reporting plan → facility → FHIR endpoint → patients of interest → organization →
 * HSLOC → encounters → a test report → MRN intake → the recap.
 *
 * Unlike the specs in `e2e/mocked`, this exists to be **watched**. It runs in the `demo` project
 * (headed, slowMo-paced, no test timeout), records a `.webm`, and holds the browser open at the
 * end.
 *
 *   npm run e2e:demo                              # default pacing, stays open, records video
 *   $env:E2E_SLOW_MO='1800'; npm run e2e:demo     # slower individual actions
 *   $env:E2E_STEP_PAUSE='3000'; npm run e2e:demo  # longer dwell between stages
 *   $env:E2E_KEEP_OPEN='0'; npm run e2e:demo      # run straight through and close
 *   $env:E2E_VIDEO='off'; npm run e2e:demo        # skip the recording
 *
 * To check a change to this file without sitting through it, run it at zero pacing:
 *   $env:E2E_KEEP_OPEN='0'; $env:E2E_SLOW_MO='0'; $env:E2E_STEP_PAUSE='0'; npm run e2e:demo
 * The browser is still visible — the `demo` project forces `headless: false` and there is no
 * flag to force it back.
 *
 * **It does not walk steps 1 → 13 in one unbroken run, and it cannot.** Step 11 is a dead end in
 * mock mode: `MockApiClient.listReports` returns a hard-coded empty page and never records what
 * `requestReport` built, so no report ever reaches the list, and the acknowledgement Continue
 * needs is fetched per report id. The walkthrough therefore stops at 11, shows that wall
 * deliberately, and re-enters at step 12 from a seeded draft — the same thing the specs for
 * steps 12 and 13 do. LEGLINK-1771 is the enabler that would remove the break; when it lands,
 * `stage('the wall at Report Results …')` and `stage('re-entering at MRN Intake …')` collapse
 * into a plain Continue.
 *
 * Three stages show behaviour that is filed as a defect rather than intended (LEGLINK-1762,
 * LEGLINK-1768). Their titles say so, so that nobody watching mistakes them for a feature.
 */

/** How long to linger on each finished stage, so it reads as steps rather than one blur. */
const STEP_PAUSE = Number(process.env.E2E_STEP_PAUSE ?? 1500);

/**
 * `test.step`, plus a dwell afterwards. `slowMo` only spaces out individual actions — without
 * this the stages run into one another and the screen is hard to follow.
 */
async function stage(title: string, body: () => Promise<void>): Promise<void> {
  await test.step(title, async () => {
    await body();
    if (STEP_PAUSE > 0) {
      await new Promise((resolve) => setTimeout(resolve, STEP_PAUSE));
    }
  });
}

/** What this facility calls itself on screen. Epic, so every vendor-branched field is Epic's. */
const VENDOR = 'Epic';
const FHIR_BASE_URL = 'https://ehr.example.org/fhir/r4';
const MEASURE = 'Glycemic Control (simulated)';

/**
 * The draft the re-entry at step 12 lands on: a facility that has been through everything above,
 * with the acknowledgement step 11 could not give it. Built from the same fixtures the mocked
 * specs use, so it stays in step with `FacilityDraft` rather than drifting as a literal.
 */
function facilityThatFinishedStepEleven() {
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

/** `census-list-<key>` — the id `CensusStep` gives each of the six list fields. */
function listField(page: Page, key: string) {
  return page.locator(`#census-list-${key}`);
}

/** Kendo's NumericTextBox commits on blur, so a typed value needs a Tab before Continue. */
async function setNumber(page: Page, id: string, value: number): Promise<void> {
  await page.locator(`#${id}`).fill(String(value));
  await page.keyboard.press('Tab');
}

/** The MRN step's Yes/No questions are tab pairs, not radios. */
async function answer(page: Page, question: RegExp, choice: 'Yes' | 'No'): Promise<void> {
  await page
    .getByRole('tablist', {name: question})
    .getByRole('tab', {name: choice, exact: true})
    .click();
}

test('walkthrough: a facility enrolls, from the welcome screen to the recap', async ({
  page,
  harness,
  onboarding
}) => {
  await stage('a facility opens the onboarding wizard', async () => {
    // No seeded draft: this is a facility arriving for the first time, so the machine starts on
    // welcome of its own accord. `open()` also puts the harness into mock mode, which it does
    // not default to.
    await harness.open();

    await expect(onboarding.heading).toContainText('Welcome to DQM Onboarding');
    // Thirteen steps, and only the first is reachable — everything after it is still locked.
    await expect(onboarding.stepLabels()).toHaveCount(13);
    await expect(onboarding.stepButton(3)).toBeDisabled();
  });

  await stage('the six-month reporting plan the facility will be held to', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('reporting-plan');

    // Read-only: a preview of what NHSN will expect once enrollment finishes. Nothing to fill in,
    // but Continue stays disabled until the schedule has actually loaded.
    await expect(onboarding.stepPanel).toContainText('6-month look-ahead');
    await expect(onboarding.continueButton).toBeEnabled();
  });

  await stage('naming the EHR vendor and the time zone the facility reports in', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('facility-info');

    // A `Select` here is a Kendo FormDropDownList, not a `<select>` — `chooseOption` opens it and
    // clicks the option out of the page-level popup.
    await chooseOption(page, 'facilityEhrVendor', VENDOR);
    // Matched on the IANA id: the rendered label carries a UTC offset computed from today's date,
    // so it reads -04:00 in summer and -05:00 in winter.
    await chooseOption(page, 'facilityTimeZone', /^America\/New_York/);
  });

  await stage('the offline route, which this facility skips', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('manual-upload');

    // Optional by design — its own copy invites uploading a partly filled form and finishing
    // later. Shown rather than used, because the import package is assembled by the BFF and mock
    // mode has nothing to serve.
    await expect(page.getByRole('button', {name: 'Download Import Sheet & Instructions'})).toBeVisible();
    await expect(page.getByRole('button', {name: 'Choose File'})).toBeVisible();
  });

  await stage('pointing NHSNLink at the FHIR endpoint, and testing it', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('fhir');

    // Vendor-branched copy: the JWKS paragraph is Epic's, served from the vendor profile rather
    // than chosen by a `vendor === 'Epic'` branch in the component.
    await expect(onboarding.stepPanel).toContainText('JSON Web Key Set (JWKS) based authentication');

    await page.getByRole('textbox', {name: 'FHIR Server Base URL'}).fill(FHIR_BASE_URL);
    await setNumber(page, 'maxConcurrentRequests', 4);
    // The lag is Days/Hours/Minutes and at least one has to be non-zero.
    await setNumber(page, 'lagMinutes', 30);

    await page.getByRole('button', {name: 'Test Connection'}).click();
    // Announced politely, as `role="status"` rather than an alert.
    await expect(onboarding.statuses).toContainText('Connection test succeeded.');
  });

  await stage('the six Epic patient lists, and how often to pull them', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('census');

    // Six FHIR List ids because the vendor profile says `censusAcquisition: 'PatientList'`. A
    // Cerner facility is asked for sFTP credentials on this same step instead.
    await expect(onboarding.stepPanel).toContainText('Epic Patient Lists');
    for (const [index, key] of CENSUS_LIST_KEYS.entries()) {
      await listField(page, key).fill(`epic-list-${index + 1}`);
    }
    await setNumber(page, 'census-frequency-hours', 1);
  });

  await stage('how NHSNLink will recognise this organization in the data', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('location-org');

    // Epic declares two resolution methods; Cerner's profile declares three different ones.
    await expect(
      page.getByRole('tablist', {name: 'Resolution Method'}).getByRole('tab', {name: 'Location Identifier'})
    ).toHaveAttribute('aria-selected', 'true');

    await page.getByRole('button', {name: /Add Location Identifier/}).click();
    await page.locator('#location-identifier-system-0').fill('urn:oid:2.16.840.1.113883.3.42');
    await page.locator('#location-identifier-code-0').fill('FAC-001');
  });

  await stage('mapping a local unit onto the HSLOC code NHSN knows it by', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('hsloc');

    await page.locator('#hsloc-location-value-0').fill('ICU-1');
    await chooseOption(page, 'hsloc-code-select-0', '1027-2 - Medical Ward (simulated)');

    // The reference tab is the other half of this step: the HSLOC catalogue, searchable, and a
    // detail panel naming which of the facility's own codes map to the selected one.
    await page.getByRole('tab', {name: /Reference/}).click();
    await expect(page.locator('.nhsn-link__hsloc-detail-code')).toHaveText('1027-2');
    await expect(page.locator('.nhsn-link__summary-list')).toContainText('ICU-1');
    // Continue from the reference tab returns to the mapping tab, so go back deliberately.
    await page.getByRole('tab', {name: /Mapping/}).first().click();
  });

  await stage('declaring which code system the local encounter types come from', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('encounter');

    // A code system on its own is a complete answer — the facility is saying where its local
    // codes come from without mapping any of them yet.
    await page
      .getByRole('textbox', {name: 'Encounter.type Code System', exact: true})
      .fill('http://mysite.org/fhir/encounter-type');
  });

  await stage('requesting a test report over one patient and one month', async () => {
    await onboarding.continueButton.click();
    await onboarding.expectOnStep('report');

    await page.locator('#reportMeasures').click();
    await page.getByRole('option', {name: MEASURE}).click();
    // Masked Kendo date inputs: `fill()` leaves `mm-dd-0006`, so the digits go in through the
    // keyboard from the first segment.
    await typeDate(page, 'reportStartDate', '10-01-2026');
    await typeDate(page, 'reportEndDate', '10-31-2026');
    await page.getByRole('button', {name: '+ Add Patient ID'}).click();
    await page.locator('#reportPatientId-0').fill('patient-001');

    // This step completes by calling the API, not by filling fields: until the request is
    // accepted the step is unfinished however complete the form looks.
    await page.getByRole('button', {name: 'Generate Report'}).click();
    await onboarding.expectOnStep('report-results');
  });

  await stage('step 11, where the walkthrough hits a wall — this is LEGLINK-1762/1771', async () => {
    // Everything on screen here is a defect, not a feature, and the two halves are separate
    // tickets.
    //
    // The subtitle promises a table. There is no table, because `listReports` is empty — and no
    // empty state either, because "No reports have been generated yet." is a `<tr>` *inside* the
    // table that only renders when there is at least one report. So the one state that message
    // exists for is the one state it cannot appear in. That is LEGLINK-1762, and it bites any
    // environment where the list comes back empty, not only this mock.
    await expect(onboarding.stepPanel).toContainText(
      'patient reporting statuses can be found in the table below'
    );
    await expect(page.locator('.nhsn-link__report-results-table')).toHaveCount(0);
    await expect(onboarding.stepPanel).not.toContainText('No reports have been generated yet.');
    // Nothing failed, so there is no error either — just blank space.
    expect(await onboarding.alertTexts()).toEqual([]);

    // Refresh is honest about having refreshed, and changes nothing.
    await page.getByRole('button', {name: /Refresh/}).click();
    await expect(page.locator('.notification-stack')).toContainText('Report list refreshed.');

    // And so the acknowledgement cannot be given, and Continue cannot be satisfied. That half is
    // mock-only — a real facility arrives here having just requested a report — and LEGLINK-1771
    // is the enabler that makes the mock record one.
    await expect(
      page.getByRole('checkbox', {name: 'I acknowledge that the generated reports are accurate.'})
    ).toBeDisabled();
    await onboarding.continueButton.click();
    expect(await onboarding.alertTexts()).toContain(
      'Please acknowledge that the generated reports are accurate before continuing.'
    );
    await onboarding.expectOnStep('report-results');
  });

  await stage('re-entering at step 12 as a facility that got past it', async () => {
    // The seam. Everything above was typed into the app; from here the draft is seeded, exactly
    // as the mocked specs for steps 12 and 13 do it. The facility is the same one — same vendor,
    // same endpoint, same report id — it simply has the acknowledgement the mock cannot grant.
    await harness.open({
      draft: facilityThatFinishedStepEleven(),
      path: '/onboarding/mrn-intake'
    });

    await expect(onboarding.heading).toContainText('MRN Identifier Intake');
  });

  await stage('answering the three MRN questions', async () => {
    await answer(page, /multiple MRN-like patient identifiers/, 'No');
    await answer(page, /vary by facility\/campus\/setting/, 'No');
    await answer(page, /change over time for the same patient/, 'No');
  });

  await stage('and adding the rule it insists on anyway — this is LEGLINK-1767', async () => {
    // The facility has just said it has one MRN. Every sibling section honours that answer and
    // disappears; the rule requirement does not, so it is asked for a rule whose own copy
    // describes it as picking the usable MRN "when multiple identifiers are present".
    await page.getByRole('button', {name: 'Complete Enrollment'}).click();
    expect(await onboarding.alertTexts()).toEqual([
      'Please add at least one rule.',
      'Please complete all fields before continuing.'
    ]);

    await page.getByRole('button', {name: '+ Add Rule'}).click();
    await page.locator('#mrn-rule-value-0').fill('http://hospital.org/fhir/identifier/mrn');
  });

  await stage('the recap of what was saved', async () => {
    await page.getByRole('button', {name: 'Complete Enrollment'}).click();
    await onboarding.expectOnStep('complete');

    // Terminal and read-only, and it re-reads the draft rather than trusting local state — so
    // this is what was actually persisted.
    await expect(onboarding.stepPanel).toContainText(
      "This facility's onboarding has been completed and saved."
    );
    const summary = page.locator('.nhsn-link__summary-list');
    await expect(summary).toContainText(VENDOR);
    await expect(summary).toContainText(FHIR_BASE_URL);
    await expect(summary).toContainText(/America\/New_York/);
    await expect(summary).toContainText(MOCK_REPORT_ID);

    await expect(onboarding.continueButton).toHaveCount(0);
    await expect(page.getByRole('button', {name: /Return to/})).toBeVisible();
  });

  await stage('two rows on that recap worth reading twice — LEGLINK-1768', async () => {
    const summary = page.locator('.nhsn-link__summary-list');

    // The connection test on step 5 was simulated, and said so in the result the mock returned.
    // `connectionTested` is a bare boolean on the draft, so the qualification is lost before it
    // reaches here and the facility is told connectivity was verified. `BffApiClient` passes the
    // same flag through unchanged, so this is not mock-only.
    await expect(summary).toContainText('Passed');

    // And these two rows read from different sources — "Reports Generated" from `listReports`,
    // "Completed From Report Id" from the draft — so they contradict each other outright here,
    // and can disagree in a real deployment while the report list catches up.
    const rows = page.locator('.nhsn-link__summary-list li');
    await expect(rows.filter({hasText: 'Reports Generated'})).toContainText('0');
    await expect(rows.filter({hasText: 'Completed From Report Id'})).toContainText(MOCK_REPORT_ID);
  });

  // Hold the window open on the finished state. `page.pause()` is not used: under the test runner
  // it is a no-op unless the run started in debug mode, so the browser would simply close.
  // Waiting on the page's own close event keeps it up until you close it — `timeout: 0` because
  // the demo project disables the test timeout too.
  if (process.env.E2E_KEEP_OPEN !== '0') {
    console.log(
      '\n  Walkthrough complete — the browser will stay open. Close the window to end the run.\n'
    );
    await page.waitForEvent('close', {timeout: 0});
  }
});
