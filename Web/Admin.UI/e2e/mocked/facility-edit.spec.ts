import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { pagedFacilities, facilityLookup, testFacilities } from '../fixtures/facilities';
import { censusConfiguration, pagedOperations } from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';
import { IFacilityConfigModel } from '../../src/app/interfaces/tenant/facility-config-model.interface';

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
  api.mock(`GET /api/data-acquisition/${facilityId}/fhirQueryConfiguration`, overrides['fhirQuery'] ?? notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/fhirQueryList`, overrides['fhirList'] ?? notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/sftp-configurations`, overrides['sftp'] ?? notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/QueryPlan`, overrides['queryPlan'] ?? notConfigured);
  api.mock(`GET /api/data-acquisition/location-config/facility/${facilityId}`, overrides['locationConfig'] ?? []);
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

  await expect.poll(() => api.callsTo('GET', `/api/data-acquisition/${facilityId}/fhirQueryConfiguration`).length).toBe(1);
  await expect.poll(() => api.callsTo('GET', `/api/data-acquisition/${facilityId}/fhirQueryList`).length).toBe(1);
  await expect.poll(() => api.callsTo('GET', `/api/data-acquisition/${facilityId}/sftp-configurations`).length).toBe(1);

  for (const label of ['Fhir Query', 'Fhir List', 'SFTP Configuration', 'Query Plan']) {
    await expect(edit.dataAcquisitionPanel.getByRole('tab', { name: label })).toBeVisible();
  }
});

test('an SFTP census can be configured as an HL7 ADT feed', async ({ page, api }) => {
  mockFacilityEdit(api);
  mockDataAcquisitionPanel(api);
  const SFTP = `/api/data-acquisition/${facilityId}/sftp-configurations`;
  api.mock(`POST ${SFTP}`, { status: 201, json: { id: 'sftp-1', organizationId: facilityId } });

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);
  await edit.dataAcquisitionPanel.getByRole('tab', { name: 'SFTP Configuration' }).click();
  await edit.dataAcquisitionPanel.getByRole('button', { name: 'Add SFTP configuration' }).click();

  const dialog = page.getByRole('dialog');
  const create = dialog.getByRole('button', { name: 'Create SFTP Configuration' });
  await dialog.getByLabel('Host').fill('sftp.example.org');
  await dialog.getByLabel('Remote Directory').fill('/adt');

  await dialog.getByRole('button', { name: 'Add Acquisition Type' }).click();
  await dialog.locator('mat-expansion-panel-header').click();

  // Nothing ADT-specific until the sub type asks for it
  await expect(dialog.getByRole('radio', { name: 'Patient MRN search' })).toHaveCount(0);

  await dialog.getByRole('combobox', { name: 'Sub Type' }).click();
  await page.getByRole('option', { name: 'HL7ADT' }).click();

  // Starts on Patient.Id on the ADT event, and neither that nor a place for read files are filled in yet
  await expect(dialog.getByLabel('Patient.Id Location')).toBeVisible();
  await expect(dialog.getByLabel('MRN Identifier System')).toHaveCount(0);
  await expect(dialog.getByText('ADT files must leave the directory once read')).toBeVisible();
  await expect(create).toBeDisabled();

  await dialog.getByLabel('Patient.Id Location').fill('not a location');
  await dialog.getByLabel('Patient.Id Location').blur();
  await expect(dialog.getByText('Enter a location such as PID-2')).toBeVisible();

  // Switching option drops the other one's error along with its field
  await dialog.getByRole('radio', { name: 'Patient MRN search' }).click();
  await expect(dialog.getByLabel('Patient.Id Location')).toHaveCount(0);
  await dialog.getByLabel('MRN Identifier System').fill('urn:oid:1.2.3');
  await dialog.getByLabel('MRN Location').fill('PID-18');
  await dialog.getByLabel('Processed Directory').fill('/adt/processed');
  await expect(dialog.getByText('ADT files must leave the directory once read')).toHaveCount(0);

  await expect(create).toBeEnabled();
  await create.click();

  await expect.poll(() => api.callsTo('POST', SFTP).length).toBe(1);
  const body = JSON.parse(api.callsTo('POST', SFTP)[0].postData!);
  expect(body.acquisitionConfigurations).toEqual([
    expect.objectContaining({
      acquisitionType: 'Census',
      subType: 'HL7ADT',
      processedDirectory: '/adt/processed',
      adtConfiguration: { mrnIdentifierSystem: 'urn:oid:1.2.3', mrnLocation: 'PID-18' },
    }),
  ]);
});

test('an existing HL7 ADT configuration is shown read-only', async ({ page, api }) => {
  mockFacilityEdit(api);
  mockDataAcquisitionPanel(api, {
    sftp: {
      id: 'sftp-1',
      organizationId: facilityId,
      host: 'sftp.example.org',
      port: 22,
      remoteDirectory: '/adt',
      timeout: '00:01:00',
      removeAfterProcessing: true,
      authenticationProtocol: 'Basic',
      enableBenchmarking: false,
      acquisitionConfigurations: [
        { acquisitionType: 'Census', subType: 'HL7ADT', adtConfiguration: { patientIdLocation: 'OBX[3.1=FHIRID]-5' } },
      ],
    },
  });
  api.mock(`GET /api/data-acquisition/${facilityId}/sftp-configurations/credentials/status`, { hasCredentials: true });

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);
  await edit.dataAcquisitionPanel.getByRole('tab', { name: 'SFTP Configuration' }).click();

  const tabPanel = edit.dataAcquisitionPanel.getByRole('tabpanel', { name: 'SFTP Configuration' });
  await tabPanel.locator('mat-expansion-panel-header', { hasText: 'HL7ADT' }).click();

  await expect(tabPanel.getByLabel('Patient.Id Location')).toHaveValue('OBX[3.1=FHIRID]-5');
  await expect(tabPanel.getByLabel('Patient.Id Location')).toBeDisabled();
  await expect(tabPanel.getByRole('radio', { name: 'Patient.Id on ADT event' })).toBeChecked();
});

test('the Reporting Organization tab is disabled until the facility has a vendor', async ({ page, api }) => {
  // The shared fixture now carries a vendor, so strip it here — the absence is the point.
  mockFacilityEdit(api, { ...facility, vendor: undefined });
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
  mockFacilityEdit(api, { ...facility, vendor: { id: 'epic-id', name: 'Epic' } });
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
