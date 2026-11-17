import { test, expect } from '../support/test';
import { reportSchedules } from '../fixtures/report-summaries';

const report = reportSchedules[0];
const path = `/sub-pre-qual-report/facility/${report.facilityId}/report/${report.id}`;
const countsPath = `/api/aggregate/reports/${report.id}/validation-category-counts`;
const issuesPath = `/api/validation/facilities/${report.facilityId}/reports/${report.id}/categories/category-1/issues`;
const category = { categoryId: 'category-1', title: 'Stored category', acceptable: false, submit: true,
  issues: 42, guidance: 'Review these findings', severity: 'ERROR' };

test.beforeEach(async ({ api }) => {
  api.mock(`GET /api/schedules/${report.id}`, report);
});

test('missing counts preserve report details and download without an empty chart', async ({ page, api }) => {
  api.mock(`GET ${countsPath}`, { status: 404, json: { detail: 'Validation counts are unavailable' } });
  await page.goto(path);
  await expect(page.getByText(report.id, { exact: true })).toBeVisible();
  await expect(page.getByRole('status')).toContainText('Counts are unavailable');
  await expect(page.getByRole('button', { name: 'Download Pre-Qual Report' })).toBeVisible();
  await expect(page.getByRole('img', { name: /Bar chart/ })).toHaveCount(0);
  expect(new URL(api.callsTo('GET', `/api/schedules/${report.id}`)[0].url).searchParams.get('includeDeleted')).toBe('true');
});

for (const hasCategories of [true, false]) {
  test(`running report displays provisional counts, categories present: ${hasCategories}`, async ({ page, api }) => {
    api.mock(`GET ${countsPath}`, { reportScheduleId: report.id, facilityId: report.facilityId,
      reportStatus: 'New', inProgress: true, lastUpdated: '', categories: hasCategories ? [category] : [] });
    await page.goto(path);
    await expect(page.getByRole('status').filter({ hasText: 'counts so far' })).toBeVisible();
    if (hasCategories) {
      await expect(page.getByRole('link', { name: category.title })).toBeVisible();
      await expect(page.getByText(category.guidance)).toBeVisible();
    } else {
      await expect(page.getByText('No counts yet.', { exact: true })).toBeVisible();
    }
  });
}

test('older reports still show saved findings when counts are missing', async ({ page, api }) => {
  api.mock(`GET ${countsPath}`, { status: 404, json: {} });
  api.mock(`GET ${issuesPath}`, { records: [{ message: 'A saved finding', expression: 'Patient.name',
    location: '', code: 'REQUIRED', patientId: 'test-patient' }],
    metadata: { pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 } });
  await page.goto(`${path}/category-1`);
  await expect(page.getByText('A saved finding', { exact: true })).toBeVisible();
  await expect(page.getByRole('status').filter({ hasText: 'Counts are unavailable' })).toBeVisible();
  await expect(page.getByText('Reported issues:', { exact: false })).toHaveCount(0);
  await expect(page.getByText('Available saved issues: 1')).toBeVisible();
});

test('uncategorized counts have guidance and no dead-end drill-down link', async ({ page, api }) => {
  api.mock(`GET ${countsPath}`, { reportScheduleId: report.id, facilityId: report.facilityId,
    reportStatus: 'Submitted', inProgress: false, lastUpdated: '',
    categories: [{ ...category, categoryId: 'uncategorized', title: 'Uncategorized', guidance: null }] });
  await page.goto(path);
  await expect(page.getByRole('cell', { name: 'Uncategorized', exact: true })).toBeVisible();
  await expect(page.getByText('These issues are not categorized and must be reviewed individually.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'Uncategorized', exact: true })).toHaveCount(0);
});

test('counts outages still show an error', async ({ page, api }) => {
  api.mock(`GET ${countsPath}`, { status: 500, json: {} });
  await page.goto(path);
  await expect(page.getByRole('alert')).toContainText('Unable to load');
  await expect(page.getByText('Counts are unavailable', { exact: false })).toHaveCount(0);
});
