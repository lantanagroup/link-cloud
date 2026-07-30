import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { FacilityEditPage } from '../support/pages/facility-edit.page';
import { testFacilities } from '../fixtures/facilities';
import { pagedOperations, queryDispatchConfiguration } from '../fixtures/facility-detail';
import { measureDefinitions } from '../fixtures/measure-defs';
import { IQueryDispatchConfiguration } from '../../src/app/interfaces/query-dispatch/query-dispatch-config-model.interface';

const facility = testFacilities[0];
const facilityId = facility.facilityId;
const QUERY_DISPATCH_GET = `/api/querydispatch/configuration/facility/${facilityId}`;
const QUERY_DISPATCH_POST = '/api/querydispatch/configuration';

const notConfigured = { status: 404, json: { detail: 'not found' } };
const newDuration = 'PT10S';

/** The edit page's own load. The query dispatch config is left to each test. */
function mockFacilityEdit(api: ApiMock): void {
  api.mock(`GET /api/facility/${facilityId}`, facility);
  api.mock('GET /api/measureeval/measure-definition', measureDefinitions);
  api.mock(`GET /api/normalization/operations/facility/${facilityId}`, pagedOperations());
}

/**
 * Create flow store: the GET 404s until the POST lands, then serves what was saved — the
 * dialog's afterClosed re-fetches, so a static fixture would leave the panel looking empty.
 * The POST body carries no `event` (see the payload assertion below), so this stands in for
 * the backend defaulting it, which is what the panel then renders.
 */
function mockQueryDispatchCreate(api: ApiMock): void {
  let saved: IQueryDispatchConfiguration | undefined;

  api.mock(`GET ${QUERY_DISPATCH_GET}`, (route) =>
    saved ? route.fulfill({ status: 200, json: saved }) : route.fulfill(notConfigured),
  );
  api.mock(`POST ${QUERY_DISPATCH_POST}`, (route) => {
    const posted = JSON.parse(route.request().postData() ?? '{}') as IQueryDispatchConfiguration;
    saved = {
      facilityId: posted.facilityId,
      dispatchSchedules: posted.dispatchSchedules.map((s) => ({ event: s.event ?? 'Discharge', duration: s.duration })),
    };
    return route.fulfill({ status: 201, json: { id: `qd-${posted.facilityId}`, message: 'Query Dispatch Created' } });
  });
}

test('a missing query dispatch configuration prompts the user to create one', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock(`GET ${QUERY_DISPATCH_GET}`, notConfigured);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.queryDispatchPanel);

  await expect(
    page.getByText(`No current query dispatch configuration found for facility ${facilityId}, please create one.`),
  ).toBeVisible();
  // As with census, the 404 branch opens the create dialog rather than leaving a dead panel.
  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog).toContainText('Query Dispatch Configuration');

  // Facility Id comes from the loaded facility and is disabled; Event is fixed at Discharge.
  await expect(dialog.getByLabel('Facility Id')).toHaveValue(facilityId);
  await expect(dialog.getByLabel('Facility Id')).toBeDisabled();
  await expect(dialog.getByLabel('Event')).toHaveValue('Discharge');
  await expect(dialog.getByLabel('Event')).toBeDisabled();

  // Duration starts empty and required, so there is nothing to save yet.
  await expect(dialog.getByLabel('Duration')).toHaveValue('');
  await expect(dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ })).toBeDisabled();
});

test('creating a query dispatch configuration posts the schedule and refreshes the panel', async ({ page, api }) => {
  mockFacilityEdit(api);
  mockQueryDispatchCreate(api);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.queryDispatchPanel);

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Scoped to the dialog: the panel renders a read-only copy of the same form behind it.
  await dialog.getByLabel('Duration').fill(newDuration);

  const create = dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ });
  await expect(create).toBeEnabled();
  await create.click();

  await expect(dialog).toBeHidden();

  const posts = api.callsTo('POST', QUERY_DISPATCH_POST);
  expect(posts).toHaveLength(1);
  // `event` is a disabled control, and Angular leaves disabled controls out of form.value —
  // so the schedule reaches the backend as duration only, with the event implied.
  expect(JSON.parse(posts[0].postData ?? '{}')).toEqual({
    facilityId,
    dispatchSchedules: [{ duration: newDuration }],
  });

  // Closing the dialog re-fetches, which is what turns the panel from "add" into "edit".
  await expect.poll(() => api.callsTo('GET', QUERY_DISPATCH_GET).length).toBe(2);
  await expect(edit.queryDispatchPanel.getByLabel('Duration')).toHaveValue(newDuration);
  await expect(edit.queryDispatchPanel.getByRole('button', { name: 'Edit query dispatch configuration' })).toBeVisible();
  await expect(edit.queryDispatchPanel.getByRole('button', { name: 'Delete query dispatch configuration' })).toBeVisible();
  await expect(edit.queryDispatchPanel.getByRole('button', { name: 'Add query dispatch configuration' })).toHaveCount(0);
});

test('a duration that is not an ISO 8601 period blocks the save', async ({ page, api }) => {
  mockFacilityEdit(api);
  mockQueryDispatchCreate(api);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.queryDispatchPanel);

  const dialog = page.getByRole('dialog');
  await dialog.getByLabel('Duration').fill('10 seconds');
  await dialog.getByLabel('Duration').blur(); // the mat-error only renders once the control is touched

  await expect(dialog.getByText('Invalid duration format. Use ISO 8601 (e.g., PT10S, PT5M, PT1H30M)')).toBeVisible();
  await expect(dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ })).toBeDisabled();
  expect(api.callsTo('POST', QUERY_DISPATCH_POST)).toHaveLength(0);

  // Correcting it re-enables the save.
  await dialog.getByLabel('Duration').fill(newDuration);
  await expect(dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ })).toBeEnabled();
});

test('an existing query dispatch configuration opens the dialog in edit mode instead', async ({ page, api }) => {
  mockFacilityEdit(api);
  api.mock(`GET ${QUERY_DISPATCH_GET}`, queryDispatchConfiguration);

  const edit = new FacilityEditPage(page);
  await edit.goto(facilityId);
  await edit.expand(edit.queryDispatchPanel);

  // No dialog is forced open when the configuration already exists.
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(edit.queryDispatchPanel.getByLabel('Duration')).toHaveValue(
    queryDispatchConfiguration.dispatchSchedules[0].duration,
  );

  await edit.queryDispatchPanel.getByRole('button', { name: 'Edit query dispatch configuration' }).click();

  const dialog = page.getByRole('dialog');
  await expect(dialog.getByRole('button', { name: /Update Query Dispatch Configuration/ })).toBeVisible();
  await expect(dialog.getByRole('button', { name: /Create Query Dispatch Configuration/ })).toHaveCount(0);
});
