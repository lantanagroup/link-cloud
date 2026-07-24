import { test, expect } from '../support/test';
import { healthSummaries, serviceInfos } from '../fixtures/health';

test('health monitor renders service status and metadata tables', async ({ page, api }) => {
  api.mock('GET /api/monitor/health', healthSummaries);
  api.mock('GET /api/info', serviceInfos);

  await page.goto('/monitor/health');

  await expect(page.locator('mat-toolbar', { hasText: 'Service Health Status' })).toBeVisible();

  const healthTable = page.locator('table').filter({ hasText: 'Health Status' });
  await expect(healthTable.locator('tr[mat-row]')).toHaveCount(healthSummaries.length);
  await expect(healthTable.locator('tr[mat-row]', { hasText: 'Validation' }).locator('mat-chip', { hasText: 'Unhealthy' }).first()).toBeVisible();

  const infoTable = page.locator('table').filter({ hasText: 'Product Version' });
  await expect(infoTable.locator('tr[mat-row]')).toHaveCount(serviceInfos.length);
  expect(api.unmatched).toHaveLength(0);
});
