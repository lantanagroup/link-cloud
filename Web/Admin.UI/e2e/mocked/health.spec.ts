import { test, expect } from '../support/test';
import { HEALTH_SERVICE_KEYS, healthByService, serviceInfos } from '../fixtures/health';

test('health monitor renders service status and metadata tables', async ({ page, api }) => {
  // One request per service rather than a single list — see the fixture.
  for (const key of HEALTH_SERVICE_KEYS) {
    api.mock(`GET /api/monitor/health/${key}`, healthByService[key]);
  }
  api.mock('GET /api/info', serviceInfos);

  await page.goto('/monitor/health');

  await expect(page.locator('mat-toolbar', { hasText: 'Health Checks' })).toBeVisible();

  // The health table is a plain table (the meta-data one above it is still a mat-table), and
  // each service contributes a row plus a hidden detail row for its entries.
  const healthTable = page.getByRole('table', { name: 'Service health checks' });
  const serviceRows = healthTable.locator('> tbody > tr:not(.detail-row)');
  await expect(serviceRows).toHaveCount(HEALTH_SERVICE_KEYS.length);
  await expect(
    serviceRows.filter({ hasText: 'Validation' }).locator('.health-status[data-status="Unhealthy"]'),
  ).toBeVisible();

  const infoTable = page.locator('table').filter({ hasText: 'Product Version' });
  await expect(infoTable.locator('tr[mat-row]')).toHaveCount(serviceInfos.length);
  expect(api.unmatched).toHaveLength(0);
});
