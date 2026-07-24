import { test, expect } from '@playwright/test';
import { NavPage } from '../support/pages/nav.page';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { assertBackendReachable, watchApiFailures } from '../support/live';

test.beforeAll(async ({ request }) => {
  await assertBackendReachable(request);
});

// Live tier: no mocks. Runs against the compose stack (UI_E2E_BASE_URL=http://localhost:8066)
// or ng serve + a locally running BFF. The stack seeds no facilities, so all assertions
// are shape-based: pages render, real API responds 2xx, no white-screens.

test('dashboard loads against the real stack without failed API responses', async ({ page }) => {
  const failures = watchApiFailures(page);

  await page.goto('/');
  await page.waitForURL('**/dashboard');
  await new NavPage(page).expectShellVisible();

  expect(failures).toEqual([]);
});

test('tenant list loads from the real BFF (rows or empty state)', async ({ page }) => {
  const failures = watchApiFailures(page);

  const tenants = new TenantDashboardPage(page);
  await tenants.goto();

  // Either facilities exist (rows) or none do (empty state) — both are healthy.
  await expect(tenants.table.or(tenants.emptyState).first()).toBeVisible();
  expect(failures).toEqual([]);
});

test('health monitor renders real service statuses', async ({ page }) => {
  await page.goto('/monitor/health');

  await expect(page.locator('mat-toolbar', { hasText: 'Service Health Status' })).toBeVisible({ timeout: 20_000 });
  // At least one service should report in a running stack.
  await expect(page.locator('table tr[mat-row]').first()).toBeVisible({ timeout: 20_000 });
});
