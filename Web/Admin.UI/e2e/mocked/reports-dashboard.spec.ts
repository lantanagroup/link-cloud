import { test, expect } from '../support/test';
import { pagedReportSchedules, reportSchedules } from '../fixtures/report-summaries';
import { facilityLookup } from '../fixtures/facilities';

test('reports dashboard renders report schedules from the summaries API', async ({ page, api }) => {
  api.mock('GET /api/aggregate/reports/summaries', pagedReportSchedules());
  api.mock('GET /api/facility/list', facilityLookup());

  await page.goto('/reports');

  const rows = page.locator('table tr[mat-row]');
  await expect(rows).toHaveCount(reportSchedules.length);
  await expect(rows.filter({ hasText: 'TestFacility01' })).toContainText('NHSNdQMAcuteCareHospitalInitialPopulation');
  await expect(rows.filter({ hasText: 'TestFacility02' })).toContainText('Monthly');
});

test('reports dashboard shows empty result set without errors', async ({ page, api }) => {
  api.mock('GET /api/aggregate/reports/summaries', pagedReportSchedules([]));
  api.mock('GET /api/facility/list', {});

  await page.goto('/reports');

  await expect(page.locator('table tr[mat-row]')).toHaveCount(0);
  expect(api.unmatched).toHaveLength(0);
});
