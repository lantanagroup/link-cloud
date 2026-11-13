import { test, expect } from '../support/test';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { facilityLookup } from '../fixtures/facilities';

// This spec exists to drive the failure path, so logged errors are the expected outcome —
// what it asserts is that the app handles them (toast shown, shell intact).
test.use({ allowConsoleErrors: true });

test('a failing facility API shows an error toast without breaking the page', async ({ page, api }) => {
  api.mock('GET /api/facility', {
    status: 500,
    json: { detail: 'Simulated backend failure', traceId: 'e2e-trace-001' },
  });
  api.mock('GET /api/facility/list', facilityLookup([]));

  const tenants = new TenantDashboardPage(page);
  await page.goto('/tenant');

  // ErrorHandlingService surfaces failures via an ngx-toastr error toast.
  await expect(page.locator('.toast-error')).toBeVisible();
  // The shell must survive: header still rendered, no rows, no white-screen.
  await expect(tenants.header).toBeVisible();
  await expect(tenants.rows).toHaveCount(0);
});
