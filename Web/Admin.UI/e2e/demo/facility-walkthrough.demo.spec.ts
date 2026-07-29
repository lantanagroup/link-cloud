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
} from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';

/**
 * A single, continuous session that walks the facility journey the way a person would:
 * tenant list → facility → filters → tabs → configuration → panels → back.
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

/** Every endpoint the walkthrough touches, in one place. */
function mockEverything(api: ApiMock): void {
  const notConfigured = { status: 404, json: { detail: 'not found' } };

  // Tenant dashboard
  api.mock('GET /api/facility', pagedFacilities());
  api.mock('GET /api/facility/list', facilityLookup());
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);

  // Facility view
  api.mock(`GET /api/facility/${facilityId}`, facility);
  api.mock('GET /api/aggregate/reports/summaries', pagedReportSchedules(schedules));
  api.mock(`GET /api/data/location-mappings/facility/${facilityId}/search`, pagedLocationMappings());
  api.mock(`GET /api/data/encounter-mappings/facilities/${facilityId}/search`, pagedEncounterMappings());

  // Facility edit
  api.mock(`GET /api/normalization/operations/facility/${facilityId}`, pagedOperations());
  api.mock(`GET /api/census/config/${facilityId}`, censusConfiguration);
  api.mock(`GET /api/querydispatch/configuration/facility/${facilityId}`, {
    facilityId,
    dispatchSchedules: [],
  });
  api.mock(`GET /api/data/${facilityId}/fhirQueryConfiguration`, {
    id: 'fq-1',
    facilityId,
    fhirServerBaseUrl: 'https://fhir.example.org/r4',
  });
  api.mock(`GET /api/data/${facilityId}/fhirQueryList`, {
    id: 'fl-1',
    facilityId,
    fhirBaseServerUrl: 'https://fhir.example.org/r4',
    ehrPatientLists: [],
  });
  api.mock(`GET /api/data/${facilityId}/sftp-configurations`, notConfigured);
  api.mock(`GET /api/data/${facilityId}/QueryPlan`, notConfigured);
  api.mock(`GET /api/data/location-config/facility/${facilityId}`, []);
}

test('walkthrough: tenant list → facility → filters → tabs → configuration', async ({ page, api }) => {
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

  // Hold the window open on the finished state. `page.pause()` is not used here: under the
  // test runner it is a no-op unless the run was started in debug mode, so the browser would
  // simply close. Waiting on the page's own close event keeps it up until you close it —
  // `timeout: 0` because the demo project disables the test timeout too.
  if (process.env['E2E_KEEP_OPEN'] !== '0') {
    console.log('\n  Walkthrough complete — the browser will stay open. Close the window to end the run.\n');
    await page.waitForEvent('close', { timeout: 0 });
  }
});
