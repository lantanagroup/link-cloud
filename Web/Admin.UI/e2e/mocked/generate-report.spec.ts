import { test, expect } from '../support/test';
import { facilityLookup, testFacilities } from '../fixtures/facilities';
import { measureDefinitions } from '../fixtures/measure-defs';
import { reportSchedules } from '../fixtures/report-summaries';
import { IReportEntrySummary } from '../../src/app/interfaces/report/report-entry.interface';

const FACILITY_ID = 'TestFacility01';
const ADHOC_POST = `/api/Facility/${FACILITY_ID}/AdHocReport`;
/** POST /Facility/{id}/AdHocReport answers with GenerateAdhocReportResponse { reportId }. */
const GENERATED_REPORT_ID = 'bbbbbbbb-0000-0000-0000-000000000001';

const emptyEntrySummary: IReportEntrySummary = {
  reportTypeCounts: {},
  reportingStatusCounts: {},
  submissionStatusCounts: {},
};

test('generate ad-hoc report submits the expected request', async ({ page, api }) => {
  api.mock('GET /api/facility/list', facilityLookup());
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock(`GET /api/facility/${FACILITY_ID}`, testFacilities[0]);
  api.mock(`POST /api/Facility/${FACILITY_ID}/AdHocReport`, { reportId: GENERATED_REPORT_ID });

  // On success the form routes straight to the new report's view page, so that page's own
  // fetches have to be answered too — the catch-all would otherwise 404 them and the
  // unmatched-/api assertion would fail the test after its payload checks had passed.
  api.mock(`GET /api/aggregate/reports/summaries/${GENERATED_REPORT_ID}`, {
    ...reportSchedules[0],
    id: GENERATED_REPORT_ID,
    facilityId: FACILITY_ID,
  });
  api.mock('GET /api/entries/search', {
    records: [],
    metadata: { pageSize: 10, pageNumber: 1, totalCount: 0, totalPages: 1 },
  });
  api.mock(`GET /api/entries/schedules/${GENERATED_REPORT_ID}/summary`, emptyEntrySummary);

  await page.goto('/reports/generate-report');

  // Facility via autocomplete
  await page.getByLabel('Facility').fill(FACILITY_ID);
  await page.getByRole('option', { name: FACILITY_ID }).click();

  // Report type multi-select
  await page.getByRole('combobox', { name: 'Report Types' }).click();
  await page.getByRole('option', { name: measureDefinitions[0].id }).click();
  await page.keyboard.press('Escape');

  // Custom cadence enables both date inputs for direct typing
  await page.getByRole('radio', { name: 'Custom' }).check();
  await page.getByLabel('Start Date').fill('6/1/2026');
  await page.getByLabel('End Date').fill('6/30/2026');

  // Census-based patient selection requires no patient list
  await page.getByRole('radio', { name: 'Use Census' }).check();

  await page.getByRole('button', { name: 'Generate Report' }).click();

  // The click fires the POST asynchronously, so the call log has to be polled — reading it
  // straight after the click races the request and fails intermittently under parallel load.
  await expect.poll(() => api.callsTo('POST', ADHOC_POST).length).toBe(1);

  const posts = api.callsTo('POST', ADHOC_POST);
  const payload = JSON.parse(posts[0].postData ?? '{}');
  expect(payload.reportTypes).toEqual([measureDefinitions[0].id]);
  expect(payload.bypassSubmission).toBe(false);
  expect(payload.startDate).toBeTruthy();
  expect(payload.endDate).toBeTruthy();

  // The response's reportId has to reach the router: when it was read off the wrong field the
  // navigation went to an `undefined` segment and only the console-error tripwire noticed.
  await expect(page).toHaveURL(new RegExp(`/tenant/facility/${FACILITY_ID}/report/${GENERATED_REPORT_ID}$`));
});
