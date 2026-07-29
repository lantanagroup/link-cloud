import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { FacilityViewPage } from '../support/pages/facility-view.page';
import { testFacilities } from '../fixtures/facilities';
import { pagedReportSchedules, reportSchedules } from '../fixtures/report-summaries';
import { locationMappings, pagedLocationMappings, pagedOperations } from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';
import { IPagedReportSchedule } from '../../src/app/interfaces/report/report-schedule.interface';

const facility = testFacilities[0];
const facilityId = facility.facilityId;
const SUMMARIES = '/api/aggregate/reports/summaries';
const LOCATION_MAPPINGS = '/api/data/location-mappings';

// The screen is scoped to one facility, so every schedule it lists belongs to that facility.
const schedules = reportSchedules.map((r) => ({ ...r, facilityId }));

/** Everything the facility view requests on load — nothing else, so the tripwire stays meaningful. */
function mockFacilityView(api: ApiMock, page: IPagedReportSchedule = pagedReportSchedules(schedules)): void {
  api.mock(`GET /api/facility/${facilityId}`, facility);
  api.mock(`GET ${SUMMARIES}`, page);
}

/** Query params of the most recent report-summaries request. */
function lastSummariesQuery(api: ApiMock): URLSearchParams {
  const calls = api.callsTo('GET', SUMMARIES);
  expect(calls.length, 'expected at least one report-summaries request').toBeGreaterThan(0);
  return new URL(calls[calls.length - 1].url).searchParams;
}

test('renders the facility name and its enrolled reporting cadences', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  await expect(view.title).toHaveText(facility.facilityName);
  await expect(view.cadenceRow('Monthly')).toContainText(facility.scheduledReports.monthly[0]);
  // Daily and Weekly are empty in the fixture, which is the branch that renders the fallback copy.
  await expect(view.cadenceRow('Daily')).toContainText('No enrolled measures');
  await expect(view.cadenceRow('Weekly')).toContainText('No enrolled measures');
});

test('the header actions expose distinct accessible names matching their labels', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  // Regression guard: Configuration and Refresh both shipped aria-label="Reload acquisition
  // logs", so screen readers announced two identical buttons and neither matched its visible
  // text. WCAG 2.5.3 wants the accessible name to contain the label the user can see.
  await expect(view.configurationButton).toHaveAccessibleName(/configuration/i);
  await expect(view.refreshButton).toHaveAccessibleName(/refresh/i);
  await expect(page.getByRole('button', { name: /back/i })).toBeVisible();

  const names = await page
    .locator('.link-card-header .right-pane button')
    .evaluateAll((buttons) => buttons.map((b) => b.getAttribute('aria-label')));
  expect(new Set(names).size, `header buttons must not share an aria-label: ${names.join(', ')}`).toBe(names.length);
});

test('lists the facility report schedules and links each id to its report', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  await expect(view.reportRows).toHaveCount(schedules.length);

  const submitted = view.reportRow(schedules[0].id);
  await expect(submitted.getByRole('link', { name: schedules[0].id })).toHaveAttribute(
    'href',
    `/tenant/facility/${facilityId}/report/${schedules[0].id}`,
  );
  await expect(submitted).toContainText(schedules[0].frequency);
  await expect(submitted).toContainText(String(schedules[0].censusCount));
  // Resubmit is offered only for reports that already reached Submitted.
  await expect(submitted.getByRole('button', { name: /Resubmit/ })).toBeVisible();
  await expect(view.reportRow(schedules[1].id).getByRole('button', { name: /Resubmit/ })).toHaveCount(0);
});

test('filtering by status refetches the schedules with that status', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);
  expect(lastSummariesQuery(api).getAll('status')).toEqual([]);

  await view.statusFilter.click();
  await page.getByRole('option', { name: 'Submitted', exact: true }).click();
  await page.keyboard.press('Escape'); // close the multi-select overlay

  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(2);
  expect(lastSummariesQuery(api).getAll('status')).toEqual(['Submitted']);
});

test('showing deleted reports refetches with includeDeleted and reveals the Deleted column', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);
  expect(lastSummariesQuery(api).get('includeDeleted')).toBe('false');
  await expect(view.reportsTable.getByRole('columnheader', { name: 'Deleted' })).toHaveCount(0);

  await view.showDeletedCheckbox.check();

  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(2);
  expect(lastSummariesQuery(api).get('includeDeleted')).toBe('true');
  await expect(view.reportsTable.getByRole('columnheader', { name: 'Deleted' })).toBeVisible();
});

test('the report id search debounces into a single request', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  await view.reportIdFilter.fill(schedules[0].id);

  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(2);
  expect(lastSummariesQuery(api).get('reportScheduleId')).toBe(schedules[0].id);

  // The 300ms debounce is the point of the test: prove no follow-up request lands behind it.
  await page.waitForTimeout(800);
  expect(api.callsTo('GET', SUMMARIES)).toHaveLength(2);
});

test('Clear shows only while filters are active and resets them', async ({ page, api }) => {
  mockFacilityView(api);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);
  await expect(view.clearFiltersButton).toHaveCount(0);

  await view.frequencyFilter.click();
  await page.getByRole('option', { name: 'Monthly', exact: true }).click();

  await expect(view.clearFiltersButton).toBeVisible();
  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(2);
  expect(lastSummariesQuery(api).get('frequency')).toBe('Monthly');

  await view.clearFiltersButton.click();

  await expect(view.clearFiltersButton).toHaveCount(0);
  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(3);
  expect(lastSummariesQuery(api).get('frequency')).toBeNull();
});

test('an empty schedule list renders the no-reports message', async ({ page, api }) => {
  mockFacilityView(api, pagedReportSchedules([]));

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  await expect(view.reportRows).toHaveCount(0);
  await expect(view.noReportsMessage).toBeVisible();
});

test('paging requests the next page from the summaries API', async ({ page, api }) => {
  const firstPage = pagedReportSchedules(schedules);
  firstPage.metadata = { ...firstPage.metadata, totalCount: 25, totalPages: 3 };
  mockFacilityView(api, firstPage);

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);
  expect(lastSummariesQuery(api).get('pageNumber')).toBe('1');

  await view.nextPageButton.click();

  await expect.poll(() => api.callsTo('GET', SUMMARIES).length).toBe(2);
  expect(lastSummariesQuery(api).get('pageNumber')).toBe('2');
});

test('Configuration navigates to the facility edit screen', async ({ page, api }) => {
  mockFacilityView(api);
  // The edit route loads on arrival, so its own endpoints have to be answered too.
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock(`GET /api/normalization/operations/facility/${facilityId}`, pagedOperations());

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);
  await view.configurationButton.click();

  await expect(page).toHaveURL(new RegExp(`/tenant/facility/${facilityId}/edit$`));
  await expect(page.getByTestId('facility-edit-header')).toHaveText(`Facility: ${facilityId}`);
});

test('the Locations tab loads its mappings only once opened', async ({ page, api }) => {
  mockFacilityView(api);
  api.mock(`GET ${LOCATION_MAPPINGS}/facility/${facilityId}/search`, pagedLocationMappings());

  const view = new FacilityViewPage(page);
  await view.goto(facilityId);

  // The tab body sits behind an *ngTemplate matTabContent — nothing is fetched until it is shown.
  expect(api.callsTo('GET', LOCATION_MAPPINGS)).toHaveLength(0);

  await view.tab('Locations').click();

  await expect.poll(() => api.callsTo('GET', LOCATION_MAPPINGS).length).toBe(1);
  // Cell-scoped: the alias "SURG" is also a substring of its row's other columns.
  await expect(page.getByRole('cell', { name: locationMappings[0].locationName!, exact: true })).toBeVisible();
  await expect(page.getByRole('cell', { name: locationMappings[1].locationAlias!, exact: true })).toBeVisible();
});
