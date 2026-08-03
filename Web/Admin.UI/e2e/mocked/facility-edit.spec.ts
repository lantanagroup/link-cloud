import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { pagedFacilities, facilityLookup, testFacilities } from '../fixtures/facilities';
import { censusConfiguration, pagedOperations } from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';
import { IFacilityConfigModel } from '../../src/app/interfaces/tenant/facility-config-model.interface';
import { Vendor } from '../../src/app/interfaces/tenant/vendor.enum';

const facility = testFacilities[0];
const facilityId = facility.facilityId;
const CENSUS = `/api/census/config/${facilityId}`;
const QUERY_DISPATCH = `/api/querydispatch/configuration/facility/${facilityId}`;

/**
 * The edit page's own load: the facility itself, the measure list behind the config form's
 * scheduled-report selects, and the normalization operations list (which is not lazy).
 * Panel configurations are deliberately left unmocked — each test adds what it expands.
 */
function mockFacilityEdit(api: ApiMock, config: IFacilityConfigModel = facility): void {
  api.mock(`GET /api/facility/${facilityId}`, config);
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock(`GET /api/normalization/operations/facility/${facilityId}`, pagedOperations());
}

/**
 * Expanding Data Acquisition fires every one of its tabs' loads at once — FHIR query, FHIR
 * list, SFTP, query plan and reporting-organization — whatever the facility's vendor is.
 * 404 is the app's "not configured yet" path, which is the state most of these tests want.
 */
function mockDataAcquisitionPanel(api: ApiMock, overrides: Record<string, object> = {}): void {
  const notConfigured = { status: 404, json: { detail: 'not found' } };
  api.mock(`GET /api/data/${facilityId}/fhirQueryConfiguration`, overrides['fhirQuery'] ?? notConfigured);
  api.mock(`GET /api/data/${facilityId}/fhirQueryList`, overrides['fhirList'] ?? notConfigured);
  api.mock(`GET /api/data/${facilityId}/sftp-configurations`, overrides['sftp'] ?? notConfigured);
  api.mock(`GET /api/data/${facilityId}/QueryPlan`, overrides['queryPlan'] ?? notConfigured);
  api.mock(`GET /api/data/location-config/facility/${facilityId}`, overrides['locationConfig'] ?? []);
}

test('shows the facility in a read-only configuration form', async ({ page, api }) => {
  mockFacilityEdit(api);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);

  await expect(edit.header).toHaveText(`Facility: ${facilityId}`);
  await expect(page.getByLabel('Facility Name')).toHaveValue(facility.facilityName);
  await expect(page.getByLabel('Facility Name')).toBeDisabled();
});

test('panel configurations are not fetched until their panel is expanded', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock(`GET ${CENSUS}`, censusConfiguration);
  api.mock(`GET ${QUERY_DISPATCH}`, { facilityId, dispatchSchedules: [] });

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);

  expect(api.callsTo('GET', CENSUS)).toHaveLength(0);
  expect(api.callsTo('GET', QUERY_DISPATCH)).toHaveLength(0);

  await edit.expand(edit.censusPanel);

  await expect.poll(() => api.callsTo('GET', CENSUS).length).toBe(1);
  // Expanding one panel must not drag the others' requests along with it.
  expect(api.callsTo('GET', QUERY_DISPATCH)).toHaveLength(0);
});

test('an existing census configuration offers edit and delete actions', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock(`GET ${CENSUS}`, censusConfiguration);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.censusPanel);

  await expect(edit.censusPanel.getByRole('button', { name: 'Edit census configuration' })).toBeVisible();
  await expect(edit.censusPanel.getByRole('button', { name: 'Delete census configuration' })).toBeVisible();
  await expect(edit.censusPanel.getByRole('button', { name: 'Add census configuration' })).toHaveCount(0);
});

test('a missing census configuration prompts the user to create one', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock(`GET ${CENSUS}`, { status: 404, json: { detail: 'not found' } });

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.censusPanel);

  await expect(
    page.getByText(`No current census configuration found for facility ${facilityId}, please create one.`),
  ).toBeVisible();
  // The 404 branch opens the create dialog for the user rather than leaving a dead panel.
  await expect(page.getByRole('dialog')).toBeVisible();
  await expect(page.getByRole('dialog')).toContainText('Census Configuration');
});

test('expanding data acquisition loads every configuration behind its tabs', async ({ page, api }) => {
  mockFacilityEdit(api);
  mockDataAcquisitionPanel(api, {
    fhirQuery: { id: 'fq-1', facilityId, fhirServerBaseUrl: 'https://fhir.example.org/r4' },
    fhirList: { id: 'fl-1', facilityId, fhirBaseServerUrl: 'https://fhir.example.org/r4', ehrPatientLists: [] },
  });

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);

  await expect.poll(() => api.callsTo('GET', `/api/data/${facilityId}/fhirQueryConfiguration`).length).toBe(1);
  await expect.poll(() => api.callsTo('GET', `/api/data/${facilityId}/fhirQueryList`).length).toBe(1);
  await expect.poll(() => api.callsTo('GET', `/api/data/${facilityId}/sftp-configurations`).length).toBe(1);

  for (const label of ['Fhir Query', 'Fhir List', 'SFTP Configuration', 'Query Plan']) {
    await expect(edit.dataAcquisitionPanel.getByRole('tab', { name: label })).toBeVisible();
  }
});

test('the Reporting Organization tab is disabled until the facility has a vendor', async ({ page, api }) => {
  mockFacilityEdit(api); // the base fixture has no vendor
  mockDataAcquisitionPanel(api);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);

  await expect(edit.dataAcquisitionPanel.getByRole('tab', { name: 'Reporting Organization' })).toHaveAttribute(
    'aria-disabled',
    'true',
  );
});

test('a facility with a vendor enables the Reporting Organization tab', async ({ page, api }) => {
  mockFacilityEdit(api, { ...facility, vendor: Vendor.Epic });
  mockDataAcquisitionPanel(api);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);

  const tab = edit.dataAcquisitionPanel.getByRole('tab', { name: 'Reporting Organization' });
  await expect(tab).not.toHaveAttribute('aria-disabled', 'true');

  await tab.click();

  await expect(tab).toHaveAttribute('aria-selected', 'true');
  await expect(edit.dataAcquisitionPanel.getByRole('button', { name: /reporting organization configuration/i })).toBeVisible();
});

test('Back returns to the tenant dashboard', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock('GET /api/facility', pagedFacilities());
  api.mock('GET /api/facility/list', facilityLookup());

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.backButton.click();

  await expect(page).toHaveURL(/\/tenant$/);
  await expect(new TenantDashboardPage(page).rowFor(facilityId)).toBeVisible();
});
