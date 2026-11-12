import { test, expect } from '@playwright/test';
import { apiBaseUrl, assertBackendReachable } from '../support/live';
import { injectAppConfig } from '../support/app-config';
import type { IReportCategoryCounts } from '../../src/app/components/tenant/facility-view/report-view.interface';

test('prequalification reads BFF counts and drills into saved findings on the live stack', async ({ page, request }) => {
  await assertBackendReachable(request);
  const api = apiBaseUrl();
  const summaries = await request.get(`${api}/aggregate/reports/summaries?pageSize=20&includeDeleted=true`);
  expect(summaries.ok()).toBe(true);
  const reports = (await summaries.json()).records as { id: string; facilityId: string }[];
  let sample: IReportCategoryCounts | undefined;
  for (const report of reports) {
    const response = await request.get(`${api}/aggregate/reports/${report.id}/validation-category-counts`);
    if (response.status() === 404) continue;
    expect(response.ok()).toBe(true);
    const counts = await response.json() as IReportCategoryCounts;
    if (counts.categories.some(category => category.categoryId.toLowerCase() !== 'uncategorized')) {
      sample = counts;
      break;
    }
  }
  test.skip(!sample, 'Requires an existing report with stored categorized counts.');
  const counts = sample!;
  const category = counts.categories.find(category => category.categoryId.toLowerCase() !== 'uncategorized')!;
  await injectAppConfig(page, { baseApiUrl: api });
  await page.goto(`/sub-pre-qual-report/facility/${counts.facilityId}/report/${counts.reportScheduleId}`);
  await expect(page.getByRole('link', { name: category.title, exact: true })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
  await page.getByRole('link', { name: category.title, exact: true }).click();
  await expect(page.getByText(`Reported issues: ${category.issues.toLocaleString('en-US')}`, { exact: true })).toBeVisible();
  await expect(page.getByText('Available saved issues:', { exact: false })).toBeVisible();
  await expect(page.getByRole('alert')).toHaveCount(0);
});
