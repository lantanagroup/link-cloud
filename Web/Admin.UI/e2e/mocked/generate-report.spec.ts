import { test, expect } from '../support/test';
import { facilityLookup, testFacilities } from '../fixtures/facilities';
import { measureDefinitions } from '../fixtures/measure-defs';

test('generate ad-hoc report submits the expected request', async ({ page, api }) => {
  api.mock('GET /api/facility/list', facilityLookup());
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock('GET /api/facility/TestFacility01', testFacilities[0]);
  api.mock('POST /api/Facility/TestFacility01/AdHocReport', {
    id: 'bbbbbbbb-0000-0000-0000-000000000001',
    message: 'Report generation requested',
  });

  await page.goto('/reports/generate-report');

  // Facility via autocomplete
  await page.getByLabel('Facility').fill('TestFacility01');
  await page.getByRole('option', { name: 'TestFacility01' }).click();

  // Report type multi-select
  await page.getByRole('combobox', { name: 'Report Types' }).click();
  await page.getByRole('option', { name: measureDefinitions[0].id }).click();
  await page.keyboard.press('Escape');

  // Custom cadence enables both date inputs for direct typing
  await page.getByRole('radio', { name: 'Custom' }).check();
  await page.getByLabel('Start Date').fill('6/1/2026');
  await page.getByLabel('End Date').fill('6/30/2026');

  // Census-based patient selection requires no patient list
  await page.getByRole('radio', { name: 'Use Census' }).check();

  await page.getByRole('button', { name: 'Generate Report' }).click();

  const posts = api.callsTo('POST', '/api/Facility/TestFacility01/AdHocReport');
  expect(posts).toHaveLength(1);
  const payload = JSON.parse(posts[0].postData ?? '{}');
  expect(payload.reportTypes).toEqual([measureDefinitions[0].id]);
  expect(payload.bypassSubmission).toBe(false);
  expect(payload.startDate).toBeTruthy();
  expect(payload.endDate).toBeTruthy();
});
