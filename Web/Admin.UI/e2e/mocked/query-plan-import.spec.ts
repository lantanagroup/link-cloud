import * as fs from 'fs';
import * as path from 'path';

import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { testFacilities } from '../fixtures/facilities';
import { pagedOperations } from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';

/**
 * Importing a query plan JSON file into the Query Plan dialog.
 *
 * The file format is PascalCase: it is what this form's own Export JSON writes, and what the
 * plans under Scripts/seed_data are. The API, by contrast, serialises camelCase, and the
 * dialog's tables and client-side checks are written against camelCase. Import used to map
 * the plan's own fields and pass the query records through untouched, so a real plan imported
 * as the right *number* of blank rows — right until someone looked at the screen.
 */

const facility = testFacilities[0];
const facilityId = facility.facilityId;
const QUERY_PLAN_POST = `/api/data-acquisition/${facilityId}/QueryPlan`;

/** The plan Scripts/seed.py seeds environments with: 2 initial queries, 10 supplemental. */
const SEED_QUERY_PLAN = path.resolve(__dirname, '../../../../Scripts/seed_data/query_plan.json');

const notConfigured = { status: 404, json: { detail: 'not found' } };

function mockFacilityEdit(api: ApiMock): void {
  api.mock(`GET /api/facility/${facilityId}`, facility);
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock(`GET /api/normalization/operations/facility/${facilityId}`, pagedOperations());

  // Expanding Data Acquisition fires every tab's load at once. Only the query plan matters
  // here; the rest are "not configured yet", which is the state that offers an Add button.
  api.mock(`GET /api/data-acquisition/${facilityId}/fhirQueryConfiguration`, notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/fhirQueryList`, notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/sftp-configurations`, notConfigured);
  api.mock(`GET /api/data-acquisition/${facilityId}/QueryPlan`, notConfigured);
  api.mock(`GET /api/data-acquisition/location-config/facility/${facilityId}`, []);
}

/** The seed file with its {{tenant-name}} placeholder resolved, as seed.py does it. */
function seedPlanFor(id: string): { name: string; mimeType: string; buffer: Buffer } {
  const json = fs.readFileSync(SEED_QUERY_PLAN, 'utf8').replace('{{tenant-name}}', id);
  return { name: 'query_plan.json', mimeType: 'application/json', buffer: Buffer.from(json) };
}

async function openQueryPlanDialog(page: import('@playwright/test').Page, edit: FacilityEditPage) {
  await edit.goto(facilityId);
  await edit.expand(edit.dataAcquisitionPanel);
  await edit.dataAcquisitionPanel.getByRole('tab', { name: 'Query Plan' }).click();
  await edit.dataAcquisitionPanel.getByRole('combobox', { name: 'Type' }).click();
  await page.getByRole('option', { name: 'Discharge', exact: true }).click();
  await edit.dataAcquisitionPanel.getByRole('button', { name: 'Add Query Plan configuration' }).click();

  return page.getByRole('dialog');
}

test('importing a PascalCase plan file renders its queries rather than blank rows', async ({ page, api }) => {
  mockFacilityEdit(api);

  const edit = new FacilityEditPage(page);
  const dialog = await openQueryPlanDialog(page, edit);

  // The visible button, not the hidden input it clicks: Playwright intercepts the native
  // picker as a filechooser event, so this exercises the real user action.
  const [chooser] = await Promise.all([
    page.waitForEvent('filechooser'),
    dialog.getByRole('button', { name: 'Import JSON' }).click(),
  ]);
  await chooser.setFiles(seedPlanFor(facilityId));

  // The plan's own fields were always mapped; these were not.
  await expect(dialog.getByLabel('Plan Name')).toHaveValue('ach');
  await expect(dialog.getByRole('cell', { name: 'Encounter', exact: true })).toBeVisible();
  await expect(dialog.getByRole('cell', { name: 'Location', exact: true })).toBeVisible();

  // Both initial queries are Parameter queries carrying parameters. "Params: 0" on every row
  // was the symptom that the nested keys had not been normalised.
  await expect(dialog.getByRole('cell', { name: 'Params: 0' })).toHaveCount(0);
});

test('an imported plan saves with its queries intact', async ({ page, api }) => {
  mockFacilityEdit(api);

  // 404 until the POST lands, then serve what was saved. Closing the dialog re-fetches, and
  // that re-fetch subscribes without an error callback — so leaving it 404ing sends an
  // HttpErrorResponse to Angular's global handler and the console tripwire fails the test.
  let saved: Record<string, unknown> | undefined;
  api.mock(`GET ${QUERY_PLAN_POST}`, (route) =>
    saved ? route.fulfill({ status: 200, json: saved }) : route.fulfill(notConfigured),
  );
  api.mock(`POST ${QUERY_PLAN_POST}`, (route) => {
    saved = JSON.parse(route.request().postData() ?? '{}');
    return route.fulfill({ status: 201, json: saved });
  });

  const edit = new FacilityEditPage(page);
  const dialog = await openQueryPlanDialog(page, edit);

  const [chooser] = await Promise.all([
    page.waitForEvent('filechooser'),
    dialog.getByRole('button', { name: 'Import JSON' }).click(),
  ]);
  await chooser.setFiles(seedPlanFor(facilityId));
  await expect(dialog.getByRole('cell', { name: 'Encounter', exact: true })).toBeVisible();

  await dialog.getByRole('button', { name: /Create Query Plan/ }).click();
  await expect(dialog).toBeHidden();

  const posts = api.callsTo('POST', QUERY_PLAN_POST);
  expect(posts).toHaveLength(1);
  const payload = JSON.parse(posts[0].postData ?? '{}');

  // The plan's own fields go up PascalCase, as this endpoint expects, while the queries
  // inside go up camelCase — the same shape the normal edit flow sends for a plan loaded
  // from the API, which is the point: import now produces what every other path produces.
  expect(payload).toMatchObject({ FacilityId: facilityId, PlanName: 'ach', Type: 'Discharge' });
  expect(Object.keys(payload.InitialQueries)).toHaveLength(2);
  expect(Object.keys(payload.SupplementalQueries)).toHaveLength(10);

  expect(payload.InitialQueries['0']).toMatchObject({
    resourceType: 'Encounter',
    queryConfigType: 'Parameter',
  });
  expect(payload.InitialQueries['1']).toMatchObject({ resourceType: 'Location' });
  // Encounter queries on patient and date; losing the parameters would leave this empty.
  expect(payload.InitialQueries['0'].parameters.length).toBeGreaterThan(0);
});
