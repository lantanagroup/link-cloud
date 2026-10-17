import * as fs from 'fs';
import * as path from 'path';

import type { Page } from '@playwright/test';

import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { NavPage } from '../support/pages/nav.page';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { FacilityViewPage } from '../support/pages/facility-view.page';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { ReportsDashboardPage } from '../support/pages/reports-dashboard.page';
import { facilityLookup, pagedFacilities, testFacilities } from '../fixtures/facilities';
import { pagedReportSchedules, reportSchedules } from '../fixtures/report-summaries';
import { IReportSchedule } from '../../src/app/interfaces/report/report-schedule.interface';
import { ScheduleStatus } from '../../src/app/interfaces/report/schedule-status';
import { IAdHocReportRequest } from '../../src/app/interfaces/tenant/facility-config-model.interface';
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
import { vendorVersions } from '../fixtures/vendors';
import { acquisitionLogStatistics, reportEntries, reportEntrySummary } from '../fixtures/report-detail';
import { placeDemoWindow } from '../support/demo-window';

/**
 * A single, continuous session that walks the facility journey the way a person would:
 * tenant list → facility → filters → tabs → configuration → panels → back → create a new
 * facility → give it a census, a query dispatch configuration, a FHIR query configuration
 * and an imported query plan → cross-facility report list → request an ad-hoc report against
 * it → read the report back:
 * measure reports per patient, and the data acquisition statistics behind them.
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

/** The form picks a vendor *version*; the POST then carries the vendor and version id apart. */
const vendorVersion = vendorVersions[0];

/** The facility the walkthrough creates through the UI, and the census cron it hands it. */
const newFacility = {
  id: '33333333-3333-3333-3333-333333333333',
  facilityId: 'DemoFacility03',
  facilityName: 'Riverside Demo Hospital',
  timeZone: 'America/Denver',
  vendor: { id: vendorVersion.vendorId, name: vendorVersion.vendorName },
  monthlyReport: measureDefinitions[0].id,
};
const newCensusTrigger = '0 0 6 * * ?';
const newDispatchDuration = 'PT10S';
const newFhirServerUrl = 'https://fhir.riverside.example.org/r4';
/**
 * The query plan Scripts/seed.py seeds environments with — a real one, not a toy. It is a
 * template: its FacilityId is the literal `{{tenant-name}}`, which the seeder replaces with
 * the tenant it is seeding. The walkthrough performs the same substitution.
 */
const SEED_QUERY_PLAN = path.resolve(__dirname, '../../../../Scripts/seed_data/query_plan.json');
const SEED_PLAN_PLACEHOLDER = '{{tenant-name}}';

/**
 * The ad-hoc report request. POST /Facility/{id}/AdHocReport answers with a
 * GenerateAdhocReportResponse — `reportId`, not `id`; reading the wrong field routed the app
 * to an undefined segment once already (see e2e/mocked/generate-report.spec.ts).
 */
const GENERATED_REPORT_ID = 'cccccccc-0000-0000-0000-000000000001';


/**
 * The POST body, as it arrives over the wire: JSON has no Date, so the two dates the form
 * sends as Date objects reach the mock as ISO strings.
 */
type AdHocPostBody = Omit<IAdHocReportRequest, 'startDate' | 'endDate'> & {
  startDate: string;
  endDate: string;
};

/**
 * Submitting the form routes straight to the new report's view page, so that page's own
 * fetches have to be answered too — the catch-all would 404 them and the walkthrough would
 * land on a broken screen in front of an audience.
 *
 * The summary is built from the request when it arrives, mirroring what Report itself builds
 * for an ad-hoc run (DotNet/Report/Listeners/GenerateReportListener.cs): frequency `Adhoc`,
 * `adHocType` Census when no patient ids were sent and Manual when they were,
 * endOfReportPeriodJobHasRun true, submission enabled unless it was bypassed, and the period
 * carried straight from the request. Serving a stock fixture instead put a *Monthly* report
 * for *May* on screen seconds after the form asked for a custom range in June, which is the
 * sort of detail an audience reads off the screen while you are talking about something else.
 *
 * Status and the two counts are a deliberate departure from that listener. Report creates the
 * schedule as `New` with no counts and the pipeline fills them in later; this walkthrough
 * opens a report that already has measure reports, so it reports `EndOfPeriod` with counts
 * taken from the entries fixture. Otherwise the header claims nought patients directly above
 * a table listing four.
 *
 * Returns a getter rather than taking the list mock's store, so the report can also appear in
 * the report lists once it exists.
 */
function mockAdHocReport(api: ApiMock, id: string): () => IReportSchedule | undefined {
  let generated: IReportSchedule | undefined;

  api.mock(`POST /api/Facility/${id}/AdHocReport`, (route) => {
    const requested = JSON.parse(route.request().postData() ?? '{}') as AdHocPostBody;
    const patientCount = reportEntries(id, GENERATED_REPORT_ID).metadata.totalCount;

    generated = {
      id: GENERATED_REPORT_ID,
      facilityId: id,
      createDate: new Date(),
      reportStartDate: new Date(requested.startDate),
      reportEndDate: new Date(requested.endDate),
      reportTypes: requested.reportTypes,
      frequency: 'Adhoc',
      adHocType: requested.patientIds?.length ? 'Manual' : 'Census',
      status: ScheduleStatus.EndOfPeriod,
      enableSubmission: !requested.bypassSubmission,
      endOfReportPeriodJobHasRun: true,
      censusCount: patientCount,
      initialPopulationCount: patientCount,
    };

    return route.fulfill({ status: 201, json: { reportId: GENERATED_REPORT_ID } });
  });

  api.mock(`GET /api/aggregate/reports/summaries/${GENERATED_REPORT_ID}`, (route) =>
    generated ? route.fulfill({ status: 200, json: generated }) : route.fulfill(notConfigured),
  );
  api.mock('GET /api/entries/search', reportEntries(id, GENERATED_REPORT_ID));
  api.mock(`GET /api/entries/schedules/${GENERATED_REPORT_ID}/summary`, reportEntrySummary);

  // Only fetched when the Query Analysis tab is selected (onTabSelected, index 1), so an
  // unmocked one would not 404 until someone clicked that tab mid-demo.
  api.mock(
    `GET /api/data-acquisition/acquisition-logs/report/${GENERATED_REPORT_ID}/statistics`,
    acquisitionLogStatistics,
  );

  return () => generated;
}

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

/**
 * Scroll whatever is under the pointer, in steps, so the page is seen to move.
 *
 * Playwright scrolls only to the element it is about to interact with, and assertions do not
 * scroll at all — so on a long screen the walkthrough silently works on things nobody in the
 * room can see. These beats exist purely to show the rest of the screen; a single jump to the
 * bottom reads as a glitch, where a few wheel steps read as someone scrolling.
 */
async function scrollThrough(page: Page, steps = 2, delta = 800): Promise<void> {
  // Scaled off the stage pacing rather than fixed, so these beats slow down with the rest of
  // the walkthrough for a presentation and cost nothing when the pacing is turned off.
  const dwell = Math.min(600, Math.round(STEP_PAUSE / 4));
  for (let i = 0; i < steps; i += 1) {
    await page.mouse.wheel(0, delta);
    if (dwell > 0) {
      await page.waitForTimeout(dwell);
    }
  }
}

/** The same, back to the top, so the next stage starts where the audience expects. */
async function scrollBack(page: Page, steps = 2, delta = 800): Promise<void> {
  await scrollThrough(page, steps, -delta);
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

/**
 * Data acquisition for the facility the walkthrough builds — stateful, like census and query
 * dispatch: 404 until the dialog posts one, then the panel reads back what it just saved.
 *
 * mockFacilityDetail hands every facility ready-made FHIR query and FHIR list configs, so this
 * has to be registered after it to win for the new facility. Without that the Add button never
 * appears, because the panel thinks it is already configured.
 */
function mockDataAcquisitionConfigs(api: ApiMock, id: string): void {
  let fhirQuery: Record<string, unknown> | null = null;
  let fhirList: Record<string, unknown> | null = null;
  let queryPlan: Record<string, unknown> | null = null;

  api.mock(`GET /api/data-acquisition/${id}/fhirQueryConfiguration`, (route) =>
    fhirQuery ? route.fulfill({ status: 200, json: fhirQuery }) : route.fulfill(notConfigured),
  );
  api.mock('POST /api/data-acquisition/fhirQueryConfiguration', (route) => {
    fhirQuery = { ...JSON.parse(route.request().postData() ?? '{}'), id: `fq-${id}` };
    return route.fulfill({ status: 201, json: { id: `fq-${id}`, message: 'Configuration created' } });
  });

  // The FHIR *list* configuration is the other acquisition mode — query by EHR patient list
  // rather than by resource — and mockFacilityDetail hands every facility a ready-made one.
  // Left inherited, a facility created sixty seconds ago would show an EHR list pointing at
  // fhir.example.org, which quietly contradicts the empty-to-configured story the census,
  // dispatch and FHIR query stages tell. The walkthrough creates no list config, so this
  // stays 404: that path only raises a snackbar, unlike census, which opens a dialog.
  api.mock(`GET /api/data-acquisition/${id}/fhirQueryList`, (route) =>
    fhirList ? route.fulfill({ status: 200, json: fhirList }) : route.fulfill(notConfigured),
  );
  api.mock('POST /api/data-acquisition/fhirQueryList', (route) => {
    fhirList = { ...JSON.parse(route.request().postData() ?? '{}'), id: `fl-${id}` };
    return route.fulfill({ status: 201, json: { id: `fl-${id}`, message: 'Configuration created' } });
  });

  // The panel asks per plan type (?type=Daily); the mock ignores the query string, so the
  // one plan the walkthrough creates answers whichever type the tab asks for.
  api.mock(`GET /api/data-acquisition/${id}/QueryPlan`, (route) =>
    queryPlan ? route.fulfill({ status: 200, json: queryPlan }) : route.fulfill(notConfigured),
  );
  api.mock(`POST /api/data-acquisition/${id}/QueryPlan`, (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}');
    // This endpoint takes the plan's own fields PascalCase but serialises them back
    // camelCase, which is what the panel reads. Echoing the POST body would hand the panel
    // `PlanName` and `InitialQueries` and leave it rendering an empty plan — a mock that
    // lies about the API's shape rather than a bug in the app.
    queryPlan = {
      id: `qp-${id}`,
      planName: posted.PlanName,
      facilityId: posted.FacilityId,
      ehrDescription: posted.EHRDescription,
      lookBack: posted.LookBack,
      initialQueries: posted.InitialQueries,
      supplementalQueries: posted.SupplementalQueries,
      type: posted.Type,
    };
    return route.fulfill({ status: 201, json: queryPlan });
  });
}

/** Everything the view and edit screens fetch for a single facility, census aside. */
function mockFacilityDetail(
  api: ApiMock,
  id: string,
  detail: { locations?: IOrganizationLocationMappingModel[]; encounters?: IEncounterMappingModel[] } = {},
): void {
  // Facility view
  api.mock(`GET /api/data-acquisition/location-mappings/facility/${id}/search`, pagedLocationMappings(detail.locations));
  api.mock(`GET /api/data-acquisition/encounter-mappings/facilities/${id}/search`, pagedEncounterMappings(detail.encounters));

  // Facility edit
  api.mock(`GET /api/normalization/operations/facility/${id}`, pagedOperations());
  api.mock(`GET /api/data-acquisition/${id}/fhirQueryConfiguration`, {
    id: `fq-${id}`,
    facilityId: id,
    fhirServerBaseUrl: 'https://fhir.example.org/r4',
  });
  api.mock(`GET /api/data-acquisition/${id}/fhirQueryList`, {
    id: `fl-${id}`,
    facilityId: id,
    fhirBaseServerUrl: 'https://fhir.example.org/r4',
    ehrPatientLists: [],
  });
  api.mock(`GET /api/data-acquisition/${id}/sftp-configurations`, notConfigured);
  api.mock(`GET /api/data-acquisition/${id}/QueryPlan`, notConfigured);
  api.mock(`GET /api/data-acquisition/location-config/facility/${id}`, []);
}

/** Every endpoint the walkthrough touches, in one place. */
function mockEverything(api: ApiMock): void {
  mockFacilities(api);
  mockCensusConfigs(api, [censusConfiguration]);
  mockQueryDispatchConfigs(api, [queryDispatchConfiguration]);
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);

  // Assigned below, declared here so the list handler can close over it: the ad-hoc report
  // does not exist until the form is submitted, and from then on it belongs in the lists.
  let adHocReport: () => IReportSchedule | undefined = () => undefined;

  // The report table filters server-side, so answer per facility: the new one has no reports
  // until it requests one, at which point it has exactly that one.
  api.mock('GET /api/aggregate/reports/summaries', (route) => {
    const wanted = new URL(route.request().url()).searchParams.get('facilityId');
    const generated = adHocReport();
    const all = generated ? [...schedules, generated] : schedules;
    const records = wanted ? all.filter((s) => s.facilityId === wanted) : all;
    return route.fulfill({ status: 200, json: pagedReportSchedules(records) });
  });

  mockFacilityDetail(api, facilityId);
  mockFacilityDetail(api, newFacility.facilityId, { locations: [], encounters: [] });
  mockDataAcquisitionConfigs(api, newFacility.facilityId);
  adHocReport = mockAdHocReport(api, newFacility.facilityId);
}

test('walkthrough: tenant list → facility → filters → tabs → configuration → create a facility → ad-hoc report', async ({ page, api }) => {
  mockEverything(api);

  // Move to the largest external screen and say so. Without this the window opens wherever
  // Chromium puts it, which is the laptop — and hand-copied coordinates land it half off
  // the intended monitor, because Chrome's coordinate space is not the one Windows reports.
  console.log(`\n  Demo ${await placeDemoWindow(page)}\n`);

  const nav = new NavPage(page);
  const tenants = new TenantDashboardPage(page);
  const view = new FacilityViewPage(page);
  const edit = new FacilityEditPage(page);
  const reports = new ReportsDashboardPage(page);

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

    // The scheduled reports sit below the fold on this screen.
    await scrollThrough(page);
    await scrollBack(page);
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

  await stage('visit the Org Locations and Encounters tabs', async () => {
    await view.tab('Org Locations').click();
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

    // Four panels expanded makes this the longest screen in the walkthrough.
    await scrollThrough(page, 3);
    await scrollBack(page, 3);
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
    await page.getByRole('option', { name: `${vendorVersion.vendorName} - ${vendorVersion.version}`, exact: true }).click();

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
      vendorVersionId: vendorVersion.id,
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

  await stage('data acquisition is unconfigured too — give it a FHIR query configuration', async () => {
    await edit.expand(edit.dataAcquisitionPanel);
    await edit.dataAcquisitionPanel.getByRole('tab', { name: 'Fhir Query' }).click();
    await edit.dataAcquisitionPanel
      .getByRole('button', { name: 'Add Fhir Resource Query configuration' })
      .click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByLabel('Facility Id')).toHaveValue(newFacility.facilityId);
    await dialog.getByLabel('Fhir Server Base Url').fill(newFhirServerUrl);
    // Create mode renders this empty even though the form declares a default of 1, and it is
    // required — the save button stays disabled until it has a value.
    await dialog.getByRole('spinbutton', { name: 'Max Concurrent Requests' }).fill('4');
    await dialog.getByRole('button', { name: /Create Fhir Query Configuration/ }).click();
    await expect(dialog).toBeHidden();

    const posts = api.callsTo('POST', '/api/data-acquisition/fhirQueryConfiguration');
    expect(JSON.parse(posts[posts.length - 1].postData ?? '{}')).toMatchObject({
      facilityId: newFacility.facilityId,
      fhirServerBaseUrl: newFhirServerUrl,
    });
  });

  await stage("import the real seed query plan, as this facility's own", async () => {
    await edit.dataAcquisitionPanel.getByRole('tab', { name: 'Query Plan' }).click();

    // The plan type is chosen on the panel, not in the dialog: the panel's copy of the form
    // is the type selector, and the dialog opens for whatever is selected there with its own
    // Type field disabled. Discharge matches the seed file, so the import has nothing to
    // override — picking a different type here makes the dialog warn and keep this one.
    await edit.dataAcquisitionPanel.getByRole('combobox', { name: 'Type' }).click();
    await page.getByRole('option', { name: 'Discharge', exact: true }).click();

    await edit.dataAcquisitionPanel
      .getByRole('button', { name: 'Add Query Plan configuration' })
      .click();

    const dialog = page.getByRole('dialog');

    // Import the plan as this facility's, the way seed.py does it: one textual substitution
    // of {{tenant-name}} for the tenant being seeded. The file keeps its own formatting, so
    // what the dialog parses is the seed plan itself rather than a re-serialised copy.
    // Supplied from a buffer — no temp file to write, clean up, or leave behind in CI.
    const planForThisFacility = fs
      .readFileSync(SEED_QUERY_PLAN, 'utf8')
      .replace(SEED_PLAN_PLACEHOLDER, newFacility.facilityId);

    // Click the dialog's own Import JSON button rather than filling its hidden file input.
    // Setting the input directly works and asserts the same things, but nothing happens on
    // screen — no click, no visible import — so the plan appeared out of nowhere and the
    // import was the one step of the walkthrough an audience could not see. The button opens
    // a native file picker, which Playwright intercepts as a filechooser event: the OS dialog
    // never actually appears, so this stays deterministic.
    // Import JSON is a low-contrast stroked button in the dialog's bottom-left action bar,
    // while every eye in the room is on the fields at the top. Hover it first so it lights
    // up and the pointer is visibly parked on it for a beat before the click — otherwise the
    // plan simply appears and the audience never sees where it came from.
    const importButton = dialog.getByRole('button', { name: 'Import JSON' });
    await importButton.scrollIntoViewIfNeeded();
    await importButton.hover();

    const [chooser] = await Promise.all([
      page.waitForEvent('filechooser'),
      importButton.click(),
    ]);
    await chooser.setFiles({
      name: 'query_plan.json',
      mimeType: 'application/json',
      buffer: Buffer.from(planForThisFacility),
    });

    // Visible proof the file landed: the queries it brought with it are on screen, not just
    // in an assertion on the payload.
    await expect(dialog.getByRole('cell', { name: 'Encounter', exact: true })).toBeVisible();
    await expect(dialog.getByRole('cell', { name: 'Location', exact: true })).toBeVisible();

    // Ten supplemental queries arrived below the two initial ones, inside the dialog's own
    // scrolling content — worth showing, since "a real plan, not a toy" is the whole point
    // of importing this file. The wheel scrolls whatever is under the pointer, so park it
    // over the dialog first.
    await dialog.getByRole('heading', { name: 'Initial Queries' }).hover();
    await scrollThrough(page, 3);
    await expect(dialog.getByRole('cell', { name: 'MedicationRequest', exact: true })).toBeVisible();
    await scrollBack(page, 3);

    // The plan now names the facility it is being created for, so the dialog has nothing to
    // correct: the id on screen came in with the file.
    await expect(dialog.getByLabel('Plan Name')).toHaveValue('ach');
    await expect(dialog.getByLabel('Facility Id')).toHaveValue(newFacility.facilityId);

    await dialog.getByRole('button', { name: /Create Query Plan/ }).click();
    await expect(dialog).toBeHidden();

    const posts = api.callsTo('POST', `/api/data-acquisition/${newFacility.facilityId}/QueryPlan`);
    const payload = JSON.parse(posts[posts.length - 1].postData ?? '{}');

    // PascalCase, unlike every other POST in this app — the query plan model is serialised
    // differently. Pinned because it is the sort of thing that gets 'tidied up' into
    // camelCase and silently stops matching the backend.
    expect(payload).toMatchObject({
      FacilityId: newFacility.facilityId,
      PlanName: 'ach',
      Type: 'Discharge',
    });

    // Encounter and Location initially, ten supplemental resource types after them. The
    // counts alone are not proof: before the import normalised its nested keys, the right
    // *number* of queries went up with every field inside them empty, which is precisely
    // how the blank rows went unnoticed. Assert the contents.
    expect(Object.keys(payload.InitialQueries ?? {})).toHaveLength(2);
    expect(Object.keys(payload.SupplementalQueries ?? {})).toHaveLength(10);
    expect(payload.InitialQueries['0']).toMatchObject({
      resourceType: 'Encounter',
      queryConfigType: 'Parameter',
    });
    expect(payload.InitialQueries['1']).toMatchObject({ resourceType: 'Location' });
  });

  await stage('the panel now shows the imported plan', async () => {
    // Closing the dialog re-fetches, the same beat the census and dispatch panels get: the
    // plan the walkthrough just imported is rendered read-only, and Add has become Edit.
    await expect(edit.dataAcquisitionPanel.getByRole('cell', { name: 'Encounter', exact: true })).toBeVisible();
    await expect(edit.dataAcquisitionPanel.getByRole('button', { name: 'Edit Query Plan configuration' })).toBeVisible();
    await expect(edit.dataAcquisitionPanel.getByRole('button', { name: 'Add Query Plan configuration' })).toHaveCount(0);
  });

  await stage('finish on the tenant dashboard, now listing both facilities', async () => {
    await edit.backButton.click();
    await expect(page).toHaveURL(/\/tenant$/);
    await expect(tenants.rowFor(facilityId)).toBeVisible();
    await expect(tenants.rowFor(newFacility.facilityId)).toBeVisible();
  });

  await stage('open the cross-facility report list from the nav bar', async () => {
    await nav.clickNav('Reports');
    await expect(reports.header).toHaveText('All Reports');
    // Every facility's reports in one table — the screen an analyst starts from, and the
    // reason the next click is where it is rather than a URL typed from nowhere.
    await expect(reports.rows).toHaveCount(schedules.length);
  });

  await stage('request an ad-hoc report for the facility we just built', async () => {
    await reports.createAdHocButton.click();
    await expect(page).toHaveURL(/\/reports\/generate-report$/);

    await page.getByLabel('Facility').fill(newFacility.facilityId);
    await page.getByRole('option', { name: newFacility.facilityId }).click();

    await page.getByRole('combobox', { name: 'Report Types' }).click();
    await page.getByRole('option', { name: newFacility.monthlyReport }).click();
    await page.keyboard.press('Escape'); // close the multi-select overlay

    await page.getByRole('radio', { name: 'Custom' }).check();
    await page.getByLabel('Start Date').fill('6/1/2026');
    await page.getByLabel('End Date').fill('6/30/2026');
    await page.getByRole('radio', { name: 'Use Census' }).check();

    await page.getByRole('button', { name: 'Generate Report' }).click();

    // The click fires the POST asynchronously, so poll rather than reading the log straight
    // after it.
    const adhoc = `/api/Facility/${newFacility.facilityId}/AdHocReport`;
    await expect.poll(() => api.callsTo('POST', adhoc).length).toBe(1);

    const payload = JSON.parse(api.callsTo('POST', adhoc)[0].postData ?? '{}');
    expect(payload.reportTypes).toEqual([newFacility.monthlyReport]);
    expect(payload.bypassSubmission).toBe(false);
  });

  await stage('which routes straight through to the report it created', async () => {
    await expect(page).toHaveURL(
      new RegExp(`/tenant/facility/${newFacility.facilityId}/report/${GENERATED_REPORT_ID}$`),
    );

    // The report on screen is the one that was just asked for, not a stock fixture: an Adhoc
    // cadence, Census as the source because the form sent no patient ids, and the reporting
    // period the presenter typed. The browser's timezone is pinned to the facility's in
    // playwright.config.ts so these dates read the same wherever the demo is given.
    const field = (label: string) => page.locator('.data-label').filter({ hasText: label });
    await expect(field('Cadence')).toContainText('Adhoc');
    await expect(field('AdHoc Source')).toContainText('Census');
    await expect(field('Reporting Period')).toContainText('6/1/26');
    await expect(field('Reporting Period')).toContainText('6/30/26');

    // And the header agrees with the table underneath it rather than quoting a stock census.
    await expect(field('Patients in Census')).toContainText('4');
    await expect(field('Patients in IP')).toContainText('4');
  });

  await stage('the report opens on its measure reports, one row per patient', async () => {
    await expect(page.getByText('PAT-10431')).toBeVisible();
    await expect(page.getByText('PAT-10588')).toBeVisible();

    // The header fields are at the top and the patient table below them — on this screen the
    // interesting half is the one you have to scroll to.
    await scrollThrough(page);
    await scrollBack(page);
  });

  await stage('Query Analysis shows what data acquisition actually did', async () => {
    await page.getByRole('tab', { name: 'Query Analysis' }).click();
    await expect(page.getByText('Total Queries')).toBeVisible();
    await expect(page.getByText('Average Query Time')).toBeVisible();

    // The per-resource timings are under the totals.
    await scrollThrough(page);
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
