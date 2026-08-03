import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { ReportsDashboardPage } from '../support/pages/reports-dashboard.page';
import { pagedReportSchedules, reportSchedules } from '../fixtures/report-summaries';
import { facilityLookup } from '../fixtures/facilities';
import { IPagedReportSchedule } from '../../src/app/interfaces/report/report-schedule.interface';

const SUMMARIES = '/api/aggregate/reports/summaries';

/** The dashboard's own load, plus the lookup behind the facility autocomplete. */
function mockReports(api: ApiMock, paged: IPagedReportSchedule = pagedReportSchedules()): void {
  api.mock(`GET ${SUMMARIES}`, paged);
  api.mock('GET /api/facility/list', facilityLookup());
}

/** More records than a page, so the paginator has somewhere to go. */
function multiPage(): IPagedReportSchedule {
  return { records: reportSchedules, metadata: { pageSize: 10, pageNumber: 1, totalCount: 109, totalPages: 11 } };
}

/** Params of the most recent summaries request — every filter here refetches server-side. */
function lastQuery(api: ApiMock): URLSearchParams {
  const calls = api.callsTo('GET', SUMMARIES);
  expect(calls.length, 'expected at least one summaries request').toBeGreaterThan(0);
  return new URL(calls[calls.length - 1].url).searchParams;
}

test('renders report schedules from the summaries API', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await expect(reports.rows).toHaveCount(reportSchedules.length);
  await expect(reports.rows.filter({ hasText: 'TestFacility01' })).toContainText(
    'NHSNdQMAcuteCareHospitalInitialPopulation',
  );
  await expect(reports.rows.filter({ hasText: 'TestFacility02' })).toContainText('Monthly');
});

test('shows the empty state without errors when there are no reports', async ({ page, api }) => {
  mockReports(api, pagedReportSchedules([]));

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await expect(reports.rows).toHaveCount(0);
  await expect(reports.emptyState).toBeVisible();
});

test('the initial load asks for the first page, newest first', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  const q = lastQuery(api);
  // The component pages from 0 but the API is 1-based — see loadReportSchedules().
  expect(q.get('pageNumber')).toBe('1');
  expect(q.get('pageSize')).toBe('10');
  expect(q.get('sortBy')).toBe('CreateDate');
  expect(q.get('sortOrder')).toBe('1'); // 1 = descending
  expect(q.get('includeDeleted')).toBe('false');
});

test('status is a multi-select and sends one status parameter per selection', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await reports.statusFilter.click();
  // The options render spaced ("End of Period") but travel as the enum name (EndOfPeriod),
  // so they must be selected by their display text.
  await page.getByRole('option', { name: 'Submitted', exact: true }).click();
  await page.getByRole('option', { name: 'End of Period', exact: true }).click();
  await page.keyboard.press('Escape');

  // HttpParams.append, not set — repeated keys rather than a comma-joined value.
  await expect.poll(() => lastQuery(api).getAll('status').sort()).toEqual(['EndOfPeriod', 'Submitted']);
});

test('the report id filter debounces into a single request', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();
  const before = api.callsTo('GET', SUMMARIES).length;

  const term = reportSchedules[0].id.slice(0, 8);
  await reports.reportIdFilter.pressSequentially(term, { delay: 30 });

  await expect.poll(() => lastQuery(api).get('reportScheduleId')).toBe(term);
  expect(api.callsTo('GET', SUMMARIES).length - before, 'debounce should collapse the keystrokes').toBe(1);
});

test('the reporting period To date is widened to the end of that day', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await reports.periodTo.fill('6/30/2026');
  await reports.periodTo.blur();

  // Picked as local midnight, then pushed to 23:59:59.999 *local* before being serialised as
  // UTC — so the wire value can land on the following calendar day (verified live: selecting
  // 6/30 sent 2026-07-01T03:59:59.999Z from a UTC-4 browser). Asserted through a Date so the
  // check holds in any timezone rather than pinning that ISO string.
  await expect.poll(() => lastQuery(api).get('reportEndDate')).not.toBeNull();
  const end = new Date(lastQuery(api).get('reportEndDate')!);
  expect([end.getFullYear(), end.getMonth(), end.getDate()]).toEqual([2026, 5, 30]); // month is 0-based
  expect([end.getHours(), end.getMinutes(), end.getSeconds(), end.getMilliseconds()]).toEqual([23, 59, 59, 999]);
});

test('sorting maps the column to its API field and returns to the first page', async ({ page, api }) => {
  mockReports(api, { records: reportSchedules, metadata: { pageSize: 10, pageNumber: 2, totalCount: 109, totalPages: 11 } });

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await reports.sortHeader('Facility ID').click();

  await expect.poll(() => lastQuery(api).get('sortBy')).toBe('FacilityId');
  expect(lastQuery(api).get('sortOrder')).toBe('0'); // first click is ascending
  expect(lastQuery(api).get('pageNumber'), 'sorting resets paging').toBe('1');
});

test('paging requests the next page using 1-based numbering', async ({ page, api }) => {
  mockReports(api, multiPage());

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await page.getByRole('button', { name: 'Next page' }).click();

  await expect.poll(() => lastQuery(api).get('pageNumber')).toBe('2');
});

test('the chosen page size is remembered across visits', async ({ page, api }) => {
  mockReports(api, multiPage());

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await reports.setPageSize('25');
  await expect.poll(() => lastQuery(api).get('pageSize')).toBe('25');

  // Persisted under reportsDashboardPageSize and re-read in ngOnInit, so a fresh visit asks
  // for 25 without the user touching the paginator again.
  await reports.goto();
  await expect.poll(() => lastQuery(api).get('pageSize')).toBe('25');
});

test('showing deleted reports refetches with includeDeleted and reveals the Deleted column', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();
  await expect(reports.table.getByRole('columnheader', { name: 'Deleted' })).toHaveCount(0);

  await reports.showDeletedCheckbox.check();

  await expect.poll(() => lastQuery(api).get('includeDeleted')).toBe('true');
  await expect(reports.table.getByRole('columnheader', { name: 'Deleted' })).toBeVisible();
});

test('Clear All Filters shows only while filters are active and resets the query', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();
  await expect(reports.clearFiltersButton).toHaveCount(0);

  await reports.frequencyFilter.click();
  await page.getByRole('option', { name: 'Monthly', exact: true }).click();
  await expect.poll(() => lastQuery(api).get('frequency')).toBe('Monthly');
  await expect(reports.clearFiltersButton).toBeVisible();

  await reports.clearFiltersButton.click();

  await expect.poll(() => lastQuery(api).get('frequency')).toBeNull();
  await expect(reports.clearFiltersButton).toHaveCount(0);
});

test('row actions appear only for the statuses that allow them', async ({ page, api }) => {
  mockReports(api);

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  // Resubmit is Submitted-only; delete is Submitted or Scheduled and not already deleted.
  // TestFacility01 is Submitted, TestFacility02 is InProgress — so the second row is bare,
  // which reads like a rendering fault until you check the template.
  const submitted = reports.rows.filter({ hasText: 'TestFacility01' });
  const inProgress = reports.rows.filter({ hasText: 'TestFacility02' });

  await expect(submitted.getByRole('button', { name: /Resubmit/ })).toBeVisible();
  await expect(submitted.getByRole('button', { name: 'Soft delete this report' })).toBeVisible();
  await expect(inProgress.getByRole('button')).toHaveCount(0);
});

test('soft deleting a report confirms first, then refetches the list', async ({ page, api }) => {
  mockReports(api);
  const target = reportSchedules[0]; // Submitted, so it has a delete button
  api.mock(`DELETE /api/aggregate/reports/${target.id}`, { status: 204 });

  const reports = new ReportsDashboardPage(page);
  await reports.goto();
  const before = api.callsTo('GET', SUMMARIES).length;

  await reports.rows.filter({ hasText: target.facilityId }).getByRole('button', { name: 'Soft delete this report' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toContainText('soft delete this report');
  await dialog.getByRole('button', { name: 'Delete', exact: true }).click();

  // Poll rather than reading the log straight after the click — the DELETE is in flight and
  // a synchronous read races it.
  await expect.poll(() => api.callsTo('DELETE', `/api/aggregate/reports/${target.id}`).length).toBe(1);
  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBeGreaterThan(before);
});

test('a report that is still running cannot be deleted and says so', async ({ page, api }) => {
  mockReports(api);
  const target = reportSchedules[0];
  api.mock(`DELETE /api/aggregate/reports/${target.id}`, {
    status: 409,
    json: { detail: 'Report is currently in progress.' },
  });

  const reports = new ReportsDashboardPage(page);
  await reports.goto();

  await reports.rows.filter({ hasText: target.facilityId }).getByRole('button', { name: 'Soft delete this report' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Delete', exact: true }).click();

  // 409 is special-cased into its own title rather than the generic failure message.
  const alert = page.getByRole('dialog');
  await expect(alert).toContainText('Report In Progress');
  await expect(alert).toContainText('Report is currently in progress.');
});
