import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { FacilityViewPage } from '../support/pages/facility-view.page';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { facilityLookup, pagedFacilities, testFacilities } from '../fixtures/facilities';
import { pagedReportSchedules, reportSchedules } from '../fixtures/report-summaries';
import {
  censusConfiguration,
  pagedEncounterMappings,
  pagedLocationMappings,
  pagedOperations,
  queryDispatchConfiguration,
} from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';
import { ICensusConfiguration } from '../../src/app/interfaces/census/census-config-model.interface';
import { IQueryDispatchConfiguration } from '../../src/app/interfaces/query-dispatch/query-dispatch-config-model.interface';
import { IEncounterMappingModel } from '../../src/app/interfaces/data-acquisition/encounter-mapping-model.interface';
import {
  IOrganizationLocationMappingModel,
} from '../../src/app/interfaces/data-acquisition/organization-location-mapping-model.interface';
import { IFacilityConfigModel } from '../../src/app/interfaces/tenant/facility-config-model.interface';
import { Vendor } from '../../src/app/interfaces/tenant/vendor.enum';

/**
 * A single, continuous session that walks the facility journey the way a person would:
 * tenant list → facility → filters → tabs → configuration → panels → back → create a new
 * facility → give it a census and a query dispatch configuration.
 *
 * Unlike the specs in e2e/mocked, this exists to be *watched*. It runs in the `demo`
 * project (headed, slowMo-paced, no timeout) and holds the browser open at the end.
 *
 *   npm run e2e:demo                            # default pacing, stays open
 *   $env:E2E_SLOW_MO='1800'; npm run e2e:demo   # slower individual actions
 *   $env:E2E_STEP_PAUSE='3000'; npm run e2e:demo  # longer dwell between stages
 *   $env:E2E_KEEP_OPEN='0'; npm run e2e:demo    # run straight through and close
 */

const facility = testFacilities[0];
const facilityId = facility.facilityId;
const schedules = reportSchedules.map((r) => ({ ...r, facilityId }));

/** The facility the walkthrough creates through the UI, and the census cron it hands it. */
const newFacility = {
  id: '33333333-3333-3333-3333-333333333333',
  facilityId: 'DemoFacility03',
  facilityName: 'Riverside Demo Hospital',
  timeZone: 'America/Denver',
  vendor: Vendor.Epic,
  monthlyReport: measureDefinitions[0].id,
};
const newCensusTrigger = '0 0 6 * * ?';
const newDispatchDuration = 'PT10S';

/** How long to linger on each finished stage before moving on, so it reads as steps. */
const STEP_PAUSE = Number(process.env['E2E_STEP_PAUSE'] ?? 1500);

/**
 * test.step, plus a dwell afterwards. slowMo only spaces out individual actions — without
 * this the stages run into one another and the screen is hard to follow.
 */
async function stage(title: string, body: () => Promise<void>): Promise<void> {
  await test.step(title, async () => {
    await body();
    if (STEP_PAUSE > 0) {
      await new Promise((resolve) => setTimeout(resolve, STEP_PAUSE));
    }
  });
}

const notConfigured = { status: 404, json: { detail: 'not found' } };

/**
 * The tenant list, the facility lookup behind it and GET /api/facility/:id, served from one
 * mutable list so the facility created mid-walkthrough shows up in the table and can then be
 * opened — a static fixture would send the app to a facility the list never gained.
 */
function mockFacilities(api: ApiMock): void {
  const facilities: IFacilityConfigModel[] = [...testFacilities];

  api.mock('GET /api/facility', (route) => route.fulfill({ status: 200, json: pagedFacilities(facilities) }));
  api.mock('GET /api/facility/list', (route) => route.fulfill({ status: 200, json: facilityLookup(facilities) }));
  api.mock('POST /api/facility', (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}') as IFacilityConfigModel;
    facilities.push({ ...posted, id: newFacility.id, isDeleted: false });
    return route.fulfill({ status: 201, json: { id: newFacility.id, message: 'Facility Created' } });
  });
  // Registered after /api/facility/list so the exact key still wins; ApiMock prefers exact matches.
  api.mock('GET /api/facility/*', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').pop();
    const found = facilities.find((f) => f.facilityId === id);
    return found ? route.fulfill({ status: 200, json: found }) : route.fulfill(notConfigured);
  });
}

/**
 * POST /api/census/config names its facility in the body rather than the path, so one store
 * answers every facility's census endpoints. The new facility starts with none — that 404 is
 * what makes the edit page offer to create one — and serves what was saved after the POST.
 */
function mockCensusConfigs(api: ApiMock, seed: ICensusConfiguration[]): void {
  const configs = new Map(seed.map((c) => [c.facilityId, c]));

  api.mock('GET /api/census/config/*', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').pop() ?? '';
    const config = configs.get(id);
    return config ? route.fulfill({ status: 200, json: config }) : route.fulfill(notConfigured);
  });
  api.mock('POST /api/census/config', (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}') as ICensusConfiguration;
    configs.set(posted.facilityId, posted);
    return route.fulfill({
      status: 201,
      json: { id: `census-${posted.facilityId}`, message: 'Census Configuration Created' },
    });
  });
}

/**
 * Query dispatch works the same way: the POST names its facility in the body while the GET is
 * per-facility, so one store answers both facilities. The demo facility already has a
 * schedule; the new one has none, and that 404 is what makes its panel offer to create one.
 * The POST body carries no `event` — it is a disabled control, and Angular leaves disabled
 * controls out of form.value — so the store defaults it the way the backend does.
 */
function mockQueryDispatchConfigs(api: ApiMock, seed: IQueryDispatchConfiguration[]): void {
  const configs = new Map(seed.map((c) => [c.facilityId, c]));

  api.mock('GET /api/querydispatch/configuration/facility/*', (route) => {
    const id = new URL(route.request().url()).pathname.split('/').pop() ?? '';
    const config = configs.get(id);
    return config ? route.fulfill({ status: 200, json: config }) : route.fulfill(notConfigured);
  });
  api.mock('POST /api/querydispatch/configuration', (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}') as IQueryDispatchConfiguration;
    configs.set(posted.facilityId, {
      facilityId: posted.facilityId,
      dispatchSchedules: posted.dispatchSchedules.map((s) => ({ event: s.event ?? 'Discharge', duration: s.duration })),
    });
    return route.fulfill({ status: 201, json: { id: `qd-${posted.facilityId}`, message: 'Query Dispatch Created' } });
  });
}

/** Everything the view and edit screens fetch for a single facility, census aside. */
function mockFacilityDetail(
  api: ApiMock,
  id: string,
  detail: { locations?: IOrganizationLocationMappingModel[]; encounters?: IEncounterMappingModel[] } = {},
): void {
  // Facility view
  api.mock(`GET /api/data/location-mappings/facility/${id}/search`, pagedLocationMappings(detail.locations));
  api.mock(`GET /api/data/encounter-mappings/facilities/${id}/search`, pagedEncounterMappings(detail.encounters));

  // Facility edit
  api.mock(`GET /api/normalization/operations/facility/${id}`, pagedOperations());
  api.mock(`GET /api/data/${id}/fhirQueryConfiguration`, {
    id: `fq-${id}`,
    facilityId: id,
    fhirServerBaseUrl: 'https://fhir.example.org/r4',
  });
  api.mock(`GET /api/data/${id}/fhirQueryList`, {
    id: `fl-${id}`,
    facilityId: id,
    fhirBaseServerUrl: 'https://fhir.example.org/r4',
    ehrPatientLists: [],
  });
  api.mock(`GET /api/data/${id}/sftp-configurations`, notConfigured);
  api.mock(`GET /api/data/${id}/QueryPlan`, notConfigured);
  api.mock(`GET /api/data/location-config/facility/${id}`, []);
}

/** Every endpoint the walkthrough touches, in one place. */
function mockEverything(api: ApiMock): void {
  mockFacilities(api);
  mockCensusConfigs(api, [censusConfiguration]);
  mockQueryDispatchConfigs(api, [queryDispatchConfiguration]);
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);

  // The report table filters server-side, so answer per facility: the new one has no reports yet.
  api.mock('GET /api/aggregate/reports/summaries', (route) => {
    const wanted = new URL(route.request().url()).searchParams.get('facilityId');
    const records = wanted ? schedules.filter((s) => s.facilityId === wanted) : schedules;
    return route.fulfill({ status: 200, json: pagedReportSchedules(records) });
  });

  mockFacilityDetail(api, facilityId);
  mockFacilityDetail(api, newFacility.facilityId, { locations: [], encounters: [] });
}

test('walkthrough: tenant list → facility → filters → tabs → configuration → create a facility', async ({ page, api }) => {
  mockEverything(api);

  const tenants = new TenantDashboardPage(page);
  const view = new FacilityViewPage(page);
  const edit = new FacilityEditPage(page);

  await stage('open the tenant dashboard', async () => {
    await tenants.goto();
    await expect(tenants.rows).toHaveCount(testFacilities.length);
    await expect(tenants.rowFor(facilityId)).toContainText(facility.facilityName);
  });

  await stage('drill into a facility', async () => {
    await tenants.rowFor(facilityId).getByRole('link', { name: facilityId }).click();
    await expect(view.title).toHaveText(facility.facilityName);
    await expect(view.cadenceRow('Monthly')).toContainText(facility.scheduledReports.monthly[0]);
    await expect(view.reportRows).toHaveCount(schedules.length);
  });

  await stage('filter the report schedules by status, then clear', async () => {
    await view.statusFilter.click();
    await page.getByRole('option', { name: 'Submitted', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(view.clearFiltersButton).toBeVisible();

    await view.clearFiltersButton.click();
    await expect(view.clearFiltersButton).toHaveCount(0);
  });

  await stage('search by report id and toggle deleted reports', async () => {
    await view.reportIdFilter.fill(schedules[0].id);
    await expect(view.reportRows).toHaveCount(schedules.length);
    await view.clearReportIdButton.click();
    await expect(view.reportIdFilter).toHaveValue('');

    await view.showDeletedCheckbox.check();
    await expect(view.reportsTable.getByRole('columnheader', { name: 'Deleted' })).toBeVisible();
    await view.showDeletedCheckbox.uncheck();
  });

  await stage('visit the Locations and Encounters tabs', async () => {
    await view.tab('Locations').click();
    await expect(page.getByRole('cell', { name: 'LOC-001', exact: true })).toBeVisible();

    await view.tab('Encounters').click();
    await expect(page.getByRole('cell', { name: 'ENC-5001', exact: true })).toBeVisible();

    await view.tab('Facility Reports').click();
    await expect(view.reportRows.first()).toBeVisible();
  });

  await stage('open the facility configuration', async () => {
    await view.configurationButton.click();
    await expect(edit.header).toHaveText(`Facility: ${facilityId}`);
  });

  await stage('expand each configuration panel', async () => {
    await edit.expand(edit.censusPanel);
    await expect(edit.censusPanel.getByRole('button', { name: 'Edit census configuration' })).toBeVisible();

    await edit.expand(edit.queryDispatchPanel);
    await expect(edit.queryDispatchPanel.getByLabel('Duration')).toHaveValue(
      queryDispatchConfiguration.dispatchSchedules[0].duration,
    );

    await edit.expand(edit.dataAcquisitionPanel);

    for (const label of ['Fhir List', 'SFTP Configuration', 'Query Plan', 'Fhir Query']) {
      await edit.dataAcquisitionPanel.getByRole('tab', { name: label }).click();
    }
  });

  await stage('head back to the tenant dashboard', async () => {
    await edit.backButton.click();
    await expect(page).toHaveURL(/\/tenant$/);
    await expect(tenants.rowFor(facilityId)).toBeVisible();
  });

  await stage('create a new facility', async () => {
    await tenants.createButton.click();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toBeVisible();

    await dialog.getByLabel('Facility ID').fill(newFacility.facilityId);
    await dialog.getByLabel('Facility Name').fill(newFacility.facilityName);

    await dialog.getByRole('combobox', { name: 'Timezone' }).click();
    await page.getByRole('option', { name: newFacility.timeZone, exact: true }).click();

    await dialog.getByRole('combobox', { name: 'Vendor' }).click();
    await page.getByRole('option', { name: newFacility.vendor, exact: true }).click();

    await dialog.getByRole('combobox', { name: 'Monthly' }).click();
    await page.getByRole('option', { name: newFacility.monthlyReport }).click();
    await page.keyboard.press('Escape'); // close the multi-select overlay

    await dialog.getByRole('button', { name: /Create Facility Configuration/ }).click();
    await expect(dialog).toBeHidden();

    const posts = api.callsTo('POST', '/api/facility');
    expect(JSON.parse(posts[posts.length - 1].postData ?? '{}')).toMatchObject({
      facilityId: newFacility.facilityId,
      facilityName: newFacility.facilityName,
      timeZone: newFacility.timeZone,
      vendor: newFacility.vendor,
      scheduledReports: { daily: [], weekly: [], monthly: [newFacility.monthlyReport] },
    });
  });

  await stage('find the new facility in the refreshed list', async () => {
    await expect(tenants.rows).toHaveCount(testFacilities.length + 1);
    await expect(tenants.rowFor(newFacility.facilityId)).toContainText(newFacility.facilityName);
  });

  await stage('open the new facility, which has no reports yet', async () => {
    await tenants.rowFor(newFacility.facilityId).getByRole('link', { name: newFacility.facilityId }).click();
    await expect(view.title).toHaveText(newFacility.facilityName);
    await expect(view.cadenceRow('Monthly')).toContainText(newFacility.monthlyReport);
    await expect(view.noReportsMessage).toBeVisible();
  });

  await stage('open its configuration — the empty census panel offers to create one', async () => {
    await view.configurationButton.click();
    await expect(edit.header).toHaveText(`Facility: ${newFacility.facilityId}`);

    await edit.expand(edit.censusPanel);
    // The 404 from GET /api/census/config/:id opens the create dialog rather than leaving a dead panel.
    await expect(
      page.getByText(`No current census configuration found for facility ${newFacility.facilityId}, please create one.`),
    ).toBeVisible();
    await expect(page.getByRole('dialog')).toContainText('Census Configuration');
  });

  await stage('create a census configuration for it', async () => {
    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Facility Id')).toHaveValue(newFacility.facilityId);

    await dialog.getByRole('textbox', { name: 'Scheduled Trigger' }).fill(newCensusTrigger);
    await dialog.getByRole('button', { name: /Create Facility Configuration/ }).click();
    await expect(dialog).toBeHidden();

    const posts = api.callsTo('POST', '/api/census/config');
    expect(JSON.parse(posts[posts.length - 1].postData ?? '{}')).toMatchObject({
      facilityId: newFacility.facilityId,
      scheduledTrigger: newCensusTrigger,
      enabled: true,
    });
  });

  await stage('the panel now shows the saved configuration', async () => {
    await expect(edit.censusPanel.getByRole('textbox', { name: 'Scheduled Trigger' })).toHaveValue(newCensusTrigger);
    await expect(edit.censusPanel.getByRole('button', { name: 'Edit census configuration' })).toBeVisible();
    await expect(edit.censusPanel.getByRole('button', { name: 'Delete census configuration' })).toBeVisible();
    await expect(edit.censusPanel.getByRole('button', { name: 'Add census configuration' })).toHaveCount(0);
  });

  await stage('move on to query dispatch, which is empty for the same reason', async () => {
    await edit.expand(edit.queryDispatchPanel);
    await expect(
      page.getByText(
        `No current query dispatch configuration found for facility ${newFacility.facilityId}, please create one.`,
      ),
    ).toBeVisible();

    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('Query Dispatch Configuration');
    // Discharge is the only dispatch event, so the duration is all there is to fill in.
    await expect(dialog.getByLabel('Facility Id')).toHaveValue(newFacility.facilityId);
    await expect(dialog.getByLabel('Event')).toHaveValue('Discharge');
  });

  await stage('give it a dispatch schedule', async () => {
    const dialog = page.getByRole('dialog');

    await dialog.getByLabel('Duration').fill(newDispatchDuration);
    await dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ }).click();
    await expect(dialog).toBeHidden();

    const posts = api.callsTo('POST', '/api/querydispatch/configuration');
    expect(JSON.parse(posts[posts.length - 1].postData ?? '{}')).toMatchObject({
      facilityId: newFacility.facilityId,
      dispatchSchedules: [{ duration: newDispatchDuration }],
    });

    await expect(edit.queryDispatchPanel.getByLabel('Duration')).toHaveValue(newDispatchDuration);
    await expect(edit.queryDispatchPanel.getByRole('button', { name: 'Edit query dispatch configuration' })).toBeVisible();
    await expect(
      edit.queryDispatchPanel.getByRole('button', { name: 'Add query dispatch configuration' }),
    ).toHaveCount(0);
  });

  await stage('finish on the tenant dashboard, now listing both facilities', async () => {
    await edit.backButton.click();
    await expect(page).toHaveURL(/\/tenant$/);
    await expect(tenants.rowFor(facilityId)).toBeVisible();
    await expect(tenants.rowFor(newFacility.facilityId)).toBeVisible();
  });

  // Hold the window open on the finished state. `page.pause()` is not used here: under the
  // test runner it is a no-op unless the run was started in debug mode, so the browser would
  // simply close. Waiting on the page's own close event keeps it up until you close it —
  // `timeout: 0` because the demo project disables the test timeout too.
  if (process.env['E2E_KEEP_OPEN'] !== '0') {
    console.log('\n  Walkthrough complete — the browser will stay open. Close the window to end the run.\n');
    await page.waitForEvent('close', { timeout: 0 });
  }
});
