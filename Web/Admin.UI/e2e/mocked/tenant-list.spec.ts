import { test, expect } from '../support/test';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { pagedFacilities, facilityLookup, testFacilities } from '../fixtures/facilities';

test.describe('tenant list', () => {
  test('renders facilities from the paged API', async ({ page, api }) => {
    api.mock('GET /api/facility', pagedFacilities());
    api.mock('GET /api/facility/list', facilityLookup());

    const tenants = new TenantDashboardPage(page);
    await tenants.goto();

    await expect(tenants.rows).toHaveCount(testFacilities.length);
    await expect(tenants.rowFor('TestFacility01')).toContainText('General Hospital One');
    await expect(tenants.rowFor('TestFacility02')).toContainText('America/Chicago');
    expect(api.unmatched).toHaveLength(0);
  });

  test('shows the empty state when no facilities exist', async ({ page, api }) => {
    api.mock('GET /api/facility', pagedFacilities([]));
    api.mock('GET /api/facility/list', {});

    const tenants = new TenantDashboardPage(page);
    await tenants.goto();

    await expect(tenants.emptyState).toBeVisible();
    await expect(tenants.emptyState).toContainText('No tenants found');
  });

  test('requests the expected paging parameters on load', async ({ page, api }) => {
    api.mock('GET /api/facility', pagedFacilities());
    api.mock('GET /api/facility/list', facilityLookup());

    const tenants = new TenantDashboardPage(page);
    await tenants.goto();
    await expect(tenants.rows).toHaveCount(testFacilities.length);

    const listCalls = api.callsTo('GET', '/api/facility').filter((c) => !new URL(c.url).pathname.includes('/list'));
    expect(listCalls.length).toBeGreaterThan(0);
    const params = new URL(listCalls[0].url).searchParams;
    expect(params.get('pageSize')).toBe('10');
    expect(params.get('pageNumber')).toBe('1'); // UI is 0-based; service sends 1-based
    expect(params.get('includeDeleted')).toBe('false');
  });
});
