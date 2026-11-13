import {expect, test} from '../support/test';
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
 * Report Results — step 11, and the one place in the flow where mock mode cannot reach the next
 * step by using the UI. Two things combine:
 *
 * - `MockApiClient.listReports` returns a hard-coded empty page and never records what
 *   `requestReport` built, so the facility's just-requested report never appears.
 * - `ReportListView` renders its table only when `reports.length > 0`, but the "No reports have
 *   been generated yet." empty state is a `<tr>` *inside* that table — so zero reports render
 *   nothing at all, and the acknowledgement checkbox (disabled until a report exists) can never
 *   be ticked.
 *
 * The dead empty state is a production defect: any environment where `listReports` comes back
 * empty shows the same void. The unreachable acknowledgement is mock-only — a real facility
 * arrives here having just requested a report, so for them it is a lag with a Refresh button.
 *
 * These are characterisation tests. When the empty state is fixed, the first one fails.
 */

function resultsDraft(overrides: Parameters<typeof draftAt>[1] = {}) {
  return draftAt('report-results', {
    facilityInfo: facilityInfoComplete(),
    fhir: fhirComplete(),
    census: censusComplete(),
    hsloc: hslocComplete(),
    encounter: encounterComplete(),
    report: reportComplete(),
    ...overrides
  });
}

test.describe('report results step', () => {
  test.beforeEach(async ({harness}) => {
    await harness.open({draft: resultsDraft(), path: '/onboarding/report-results'});
  });

  test('shows neither the table it promises nor an empty state', async ({page, onboarding}) => {
    // The copy still points at a table.
    await expect(onboarding.stepPanel).toContainText('patient reporting statuses can be found in the table below');

    await expect(page.locator('.nhsn-link__report-results-table')).toHaveCount(0);
    await expect(onboarding.stepPanel).not.toContainText('No reports have been generated yet.');
    // No error either — nothing failed, there is just nothing on screen between the subtitle and
    // the buttons.
    expect(await onboarding.alertTexts()).toEqual([]);
  });

  test('cannot acknowledge, so refuses to continue', async ({page, onboarding}) => {
    const acknowledgement = page.getByRole('checkbox', {
      name: 'I acknowledge that the generated reports are accurate.'
    });
    await expect(acknowledgement).toBeDisabled();

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(
      'Please acknowledge that the generated reports are accurate before continuing.'
    );
    await onboarding.expectOnStep('report-results');
  });

  test('refreshing reports success and still shows nothing', async ({page, onboarding}) => {
    // Worth pinning that the void is not a load-timing artefact: the list is re-fetched, the
    // facility is told it refreshed, and the result is the same empty screen.
    await page.getByRole('button', {name: /Refresh/}).click();

    // Notifications are toasts in `.notification-stack`, not `role="status"` regions inside the
    // step — the stack carries one `aria-live` for all of them, so there is no per-toast role.
    await expect(page.locator('.notification-stack')).toContainText('Report list refreshed.');
    await expect(page.locator('.nhsn-link__report-results-table')).toHaveCount(0);
  });

  test('offers a way back to generate another report', async ({page, onboarding}) => {
    await page.getByRole('button', {name: '+ Generate New Report'}).click();

    await onboarding.expectOnStep('report');
  });

  test('is not satisfied by the acknowledgement on the draft, only by the one on the server', async ({onboarding, harness}) => {
    // The step's gate and `flow.ts`'s gate read different things. `flow.ts` asks the draft for
    // `reportResults.accuracyAcknowledged`, but Continue checks the report-scoped attestation
    // fetched with `getReportAcknowledgement(latestReportId)` — and with no report in the list
    // there is no id to fetch for, so that query never even runs.
    //
    // So the draft saying acknowledged changes nothing here. That is the real shape of the mock's
    // dead end: not "tick the box", but "there is no report to acknowledge". Seeding the draft is
    // how the specs for steps 12 and 13 arrive instead of walking through this one.
    await harness.open({
      draft: resultsDraft({reportResults: reportResultsAcknowledged()}),
      path: '/onboarding/report-results'
    });

    await onboarding.continueButton.click();

    expect(await onboarding.alertTexts()).toContain(
      'Please acknowledge that the generated reports are accurate before continuing.'
    );
    await onboarding.expectOnStep('report-results');
  });
});
