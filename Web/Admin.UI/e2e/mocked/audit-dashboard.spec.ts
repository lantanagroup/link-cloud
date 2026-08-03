import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { AuditDashboardPage } from '../support/pages/audit-dashboard.page';
import { auditEvents, pagedAuditEvents } from '../fixtures/audit';
import { facilityLookup } from '../fixtures/facilities';
import { PagedAuditModel } from '../../src/app/models/audit/paged-audit-model.model';

const AUDIT = '/api/audit';

/**
 * The screen opens with a forkJoin of the facility lookup and an unfiltered audit search,
 * so both must be mocked or the page renders nothing.
 */
function mockAudit(api: ApiMock, paged: PagedAuditModel = pagedAuditEvents()): void {
  api.mock(`GET ${AUDIT}`, paged);
  api.mock('GET /api/facility/list', facilityLookup());
}

/** Params of the most recent audit request — every filter here refetches server-side. */
function lastQuery(api: ApiMock): URLSearchParams {
  const calls = api.callsTo('GET', AUDIT);
  expect(calls.length, 'expected at least one audit request').toBeGreaterThan(0);
  return new URL(calls[calls.length - 1].url).searchParams;
}

test('renders audit events from the API', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await expect(audit.rows).toHaveCount(auditEvents.length);
  await expect(audit.rowFor(auditEvents[0].correlationId)).toContainText('SystemUser');
  await expect(audit.rowFor(auditEvents[0].correlationId)).toContainText('Tenant');
  await expect(audit.rowFor(auditEvents[1].correlationId)).toContainText('QueryDispatch');
});

test('the initial load asks for the first page with no filters applied', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  const q = lastQuery(api);
  // AuditService increments before sending: the component's 0-based page becomes 1.
  expect(q.get('pageNumber')).toBe('1');
  expect(q.get('pageSize')).toBe('10');
  for (const filter of ['searchText', 'facility', 'correlationId', 'service', 'action', 'user']) {
    expect(q.get(filter), `${filter} should be absent on first load`).toBeNull();
  }
});

test('no audit events renders the empty message instead of an empty table', async ({ page, api }) => {
  mockAudit(api, pagedAuditEvents([], 0));

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  // The table is inside an @if, so it is absent rather than present-but-empty.
  await expect(audit.table).toHaveCount(0);
  await expect(audit.emptyState).toContainText('No audit events found.');
});

test('the facility filter submits the id while showing the facility name', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  // Options are built from the facility lookup as id -> name, so the label is the name and
  // the value is the id. Selecting by label proves both halves of that mapping.
  await audit.facilityFilter.selectOption({ label: 'General Hospital One' });

  await expect.poll(() => lastQuery(api).get('facility')).toBe('TestFacility01');
});

test('the service and action filters send their own parameters', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await audit.serviceFilter.selectOption({ label: 'Tenant Service' });
  await expect.poll(() => lastQuery(api).get('service')).toBe('Tenant');

  await audit.actionFilter.selectOption({ label: 'Create' });
  await expect.poll(() => lastQuery(api).get('action')).toBe('Create');
  // Filters accumulate rather than replacing one another.
  expect(lastQuery(api).get('service')).toBe('Tenant');
});

test('text filters search on Enter, not while typing', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();
  const before = api.callsTo('GET', AUDIT).length;

  // Unlike the reports dashboard there is no debounce — the binding is (keydown.enter), so
  // typing alone must not reach the API at all.
  await audit.correlationIdFilter.fill(auditEvents[0].correlationId);
  expect(api.callsTo('GET', AUDIT).length, 'typing alone should send nothing').toBe(before);

  await audit.correlationIdFilter.press('Enter');
  await expect.poll(() => lastQuery(api).get('correlationId')).toBe(auditEvents[0].correlationId);
});

test('the user and search text filters submit on Enter too', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await audit.submitFilter(audit.userIdFilter, 'SystemUser');
  // The query parameter is `user`, not `userId` — the field label is the only place "Id" appears.
  await expect.poll(() => lastQuery(api).get('user')).toBe('SystemUser');

  await audit.submitFilter(audit.searchTextFilter, 'created');
  await expect.poll(() => lastQuery(api).get('searchText')).toBe('created');
});

test('Clear Filters empties every field and refetches unfiltered', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await audit.submitFilter(audit.correlationIdFilter, auditEvents[0].correlationId);
  await audit.facilityFilter.selectOption({ label: 'General Hospital One' });
  await expect.poll(() => lastQuery(api).get('facility')).toBe('TestFacility01');

  await audit.clearFiltersButton.click();

  await expect.poll(() => lastQuery(api).get('correlationId')).toBeNull();
  expect(lastQuery(api).get('facility')).toBeNull();
  await expect(audit.correlationIdFilter).toHaveValue('');
  await expect(audit.facilityFilter).toHaveValue('Any');
});

test('Refresh refetches while keeping the active filters', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await audit.submitFilter(audit.userIdFilter, 'SystemUser');
  const before = api.callsTo('GET', AUDIT).length;

  await audit.refreshButton.click();

  await expect.poll(() => api.callsTo('GET', AUDIT).length).toBeGreaterThan(before);
  expect(lastQuery(api).get('user'), 'refresh must not clear the filters').toBe('SystemUser');
});

test('only events that carry notes offer the notes toggle', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  const withNotes = audit.rowFor(auditEvents[0].correlationId);
  const withoutNotes = audit.rowFor(auditEvents[1].correlationId);

  // The button is inside an @if on notes.length, so the second event has no toggle at all.
  await expect(withNotes.getByRole('button', { name: 'Toggle notes' })).toBeVisible();
  await expect(withoutNotes.getByRole('button', { name: 'Toggle notes' })).toHaveCount(0);

  await withNotes.getByRole('button', { name: 'Toggle notes' }).click();
  await expect(page.getByText(auditEvents[0].notes)).toBeVisible();
});

test('paging asks the API for the next page, one-based', async ({ page, api }) => {
  mockAudit(api, pagedAuditEvents(auditEvents, 299));

  const audit = new AuditDashboardPage(page);
  await audit.goto();

  await page.getByRole('button', { name: 'Next page' }).click();

  // The component moves to its page index 1, which the service sends as 2.
  await expect.poll(() => lastQuery(api).get('pageNumber')).toBe('2');
});

test('sorting is never requested — the service drops the parameter it is given', async ({ page, api }) => {
  mockAudit(api);

  const audit = new AuditDashboardPage(page);
  await audit.goto();
  await audit.refreshButton.click();

  // getAuditLogs passes defaultSortBy ('EventDate') into searchLogs, but searchLogs never
  // appends it to the query string, so no request can ever carry it. Characterisation, not
  // endorsement: if the parameter is ever wired up, this test should start failing.
  await expect.poll(() => api.callsTo('GET', AUDIT).length).toBeGreaterThan(1);
  for (const call of api.callsTo('GET', AUDIT)) {
    expect(new URL(call.url).searchParams.get('sortBy'), 'sortBy is silently dropped').toBeNull();
  }
});
