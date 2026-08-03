import { test, expect } from '../support/test';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { pagedFacilities, facilityLookup } from '../fixtures/facilities';
import { measureDefinitions } from '../fixtures/measure-defs';

test('creating a facility posts the expected payload and closes the dialog', async ({ page, api }) => {
  api.mock('GET /api/facility', pagedFacilities());
  api.mock('GET /api/facility/list', facilityLookup());
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock('POST /api/facility', { id: '99999999-9999-9999-9999-999999999999', message: 'Facility Created' });

  const tenants = new TenantDashboardPage(page);
  await tenants.goto();
  await tenants.createButton.click();

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  await dialog.getByLabel('Facility ID').fill('NewFacility01');
  await dialog.getByLabel('Facility Name').fill('Brand New Hospital');

  await dialog.getByRole('combobox', { name: 'Timezone' }).click();
  await page.getByRole('option', { name: 'America/New_York', exact: true }).click();

  await dialog.getByRole('combobox', { name: 'Vendor' }).click();
  await page.getByRole('option', { name: 'Epic', exact: true }).click();

  await dialog.getByRole('combobox', { name: 'Monthly' }).click();
  await page.getByRole('option', { name: measureDefinitions[0].id }).click();
  await page.keyboard.press('Escape'); // close the multi-select overlay

  await dialog.getByRole('button', { name: /Create Facility Configuration/ }).click();

  await expect(dialog).toBeHidden();
  const posts = api.callsTo('POST', '/api/facility');
  expect(posts).toHaveLength(1);
  const payload = JSON.parse(posts[0].postData ?? '{}');
  expect(payload).toMatchObject({
    facilityId: 'NewFacility01',
    facilityName: 'Brand New Hospital',
    timeZone: 'America/New_York',
    vendor: 'Epic',
    scheduledReports: { daily: [], weekly: [], monthly: [measureDefinitions[0].id] },
  });
});

test('submit stays disabled until the form is valid', async ({ page, api }) => {
  api.mock('GET /api/facility', pagedFacilities());
  api.mock('GET /api/facility/list', facilityLookup());
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);

  const tenants = new TenantDashboardPage(page);
  await tenants.goto();
  await tenants.createButton.click();

  const dialog = page.getByRole('dialog');
  const submit = dialog.getByRole('button', { name: /Create Facility Configuration/ });
  await expect(submit).toBeDisabled();

  // Filling only some required fields must not enable it.
  await dialog.getByLabel('Facility ID').fill('NewFacility01');
  await dialog.getByLabel('Facility Name').fill('Brand New Hospital');
  await expect(submit).toBeDisabled();
});
