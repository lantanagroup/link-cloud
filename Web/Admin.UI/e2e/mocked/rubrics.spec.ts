import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import { NavPage } from '../support/pages/nav.page';
import {
  bareMinimumEvents,
  bareMinimumRubric,
  draftOnlyRubric,
  publishedVersion,
  rubricError,
  rubricOk,
  rubricOwners,
  rubricPage,
  rubricSettings,
} from '../fixtures/rubrics';

const RUBRICS = '/api/validation/rubrics';
const RUBRIC = `${RUBRICS}/bare-minimum-requirements`;

/** Params of the most recent list request. Status, sorting and paging are all done by the service. */
function lastListQuery(api: ApiMock): URLSearchParams {
  const calls = api.callsTo('GET', RUBRICS).filter((c) => new URL(c.url).pathname === RUBRICS);
  expect(calls.length, 'expected at least one rubric list request').toBeGreaterThan(0);
  return new URL(calls[calls.length - 1].url).searchParams;
}

// The list asks for the owners and both the list and the rubric page ask for the settings. Neither is what
// most of these tests are about, so they are answered for all of them; a test about one replaces it.
test.beforeEach(async ({ api }) => {
  api.mock(`GET ${RUBRICS}/$owners`, rubricOk(rubricOwners));
  api.mock(`GET ${RUBRICS}/$settings`, rubricOk(rubricSettings));
});

test.describe('rubric list', () => {
  test('is reached from the Configuration menu', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));
    api.mock('GET /api/facility/list', {});

    await page.goto('/dashboard');
    await new NavPage(page).clickDropdown('Configuration', 'Validation Rubrics');

    await expect(page).toHaveURL(/\/rubrics$/);
    await expect(page.getByRole('heading', { name: 'Validation Rubrics' })).toBeVisible();
  });

  test('shows each rubric with its newest published and draft versions', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric, draftOnlyRubric]));

    await page.goto('/rubrics');

    const published = page.getByRole('row', { name: /ACH Monthly Bare-Minimum Requirements/ });
    await expect(published).toContainText('bare-minimum-requirements');
    await expect(published).toContainText('lcg');
    await expect(published.getByRole('link', { name: '1.1.0' })).toBeVisible();
    await expect(published.getByRole('link', { name: '1.2.0' })).toBeVisible();

    const draftOnly = page.getByRole('row', { name: /Cross-Measure Patient/ });
    await expect(draftOnly.getByRole('link', { name: '0.3.0' })).toBeVisible();
    await expect(draftOnly.getByRole('link')).toHaveCount(2); // its title and its draft; nothing published
  });

  test('the first load asks for the first page, sorted by rubric id, with no filters', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();

    const q = lastListQuery(api);
    expect(q.get('page')).toBe('0');
    expect(q.get('size')).toBe('20');
    expect(q.get('sort')).toBe('rubricId,asc');
    for (const filter of ['status', 'search', 'owner']) {
      expect(q.get(filter), `${filter} should be absent on first load`).toBeNull();
    }
    // every version of each rubric, so a rubric found by its draft still shows what is published
    expect(q.get('allVersions')).toBe('true');
  });

  test('typing in the search asks the service once typing pauses', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();
    await page.getByLabel('Search').pressSequentially('minimum', { delay: 20 });

    await expect.poll(() => lastListQuery(api).get('search')).toBe('minimum');
    expect(lastListQuery(api).get('page')).toBe('0');
    // one request for the whole word, not one per letter
    const searches = api.callsTo('GET', RUBRICS)
      .map((c) => new URL(c.url))
      .filter((u) => u.pathname === RUBRICS && u.searchParams.has('search'));
    expect(searches).toHaveLength(1);
  });

  test('choosing an owner asks the service for it', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await page.getByLabel('Owner').selectOption('lcg');

    await expect.poll(() => lastListQuery(api).get('owner')).toBe('lcg');
  });

  test('there is no owner filter when the owners cannot be loaded, and the list still shows', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/$owners`, rubricError(500, 'An unexpected error occurred'));
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');

    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();
    await expect(page.getByLabel('Owner')).toHaveCount(0);
  });

  test('Clear filters removes every filter and reloads', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await page.getByLabel('Status').selectOption('DRAFT');
    await page.getByLabel('Owner').selectOption('lcg');
    await expect.poll(() => lastListQuery(api).get('owner')).toBe('lcg');

    await page.getByRole('button', { name: 'Clear filters' }).click();

    await expect.poll(() => lastListQuery(api).get('owner')).toBeNull();
    expect(lastListQuery(api).get('status')).toBeNull();
    await expect(page.getByLabel('Status')).toHaveValue('');
    await expect(page.getByRole('button', { name: 'Clear filters' })).toHaveCount(0);
  });

  test('filters that match nothing say so and offer to clear', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([]));

    await page.goto('/rubrics');
    await page.getByLabel('Status').selectOption('RETIRED');

    await expect(page.getByText('No rubric matches.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Clear filters' }).first()).toBeVisible();
  });

  test('the rubric the pipeline uses is marked In Use, and only that one', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric, draftOnlyRubric]));

    await page.goto('/rubrics');

    const marker = page.getByText('In Use', { exact: true });
    await expect(marker).toHaveCount(1);
    await expect(page.getByRole('row', { name: /Bare-Minimum/ }).getByText('In Use', { exact: true })).toBeVisible();
    // the fixture's pipeline only compares the rubric engine with the validator that decides
    await expect(marker).toHaveAttribute('title', /does not decide/);
  });

  test('the marker says the rubric decides when the pipeline validates with the rubric engine', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/$settings`, rubricOk({ ...rubricSettings, pipelineEnabled: true }));
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');

    await expect(page.getByText('In Use', { exact: true })).toHaveAttribute('title', /decides whether the data passes/);
  });

  test('no rubric is marked when the rubric engine does not run in the pipeline', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/$settings`, rubricOk({ ...rubricSettings, pipelineEnabled: false, pipelineShadowEnabled: false }));
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');

    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();
    await expect(page.getByText('In Use', { exact: true })).toHaveCount(0);
  });

  test('no rubric is marked when the settings cannot be loaded', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/$settings`, rubricError(500, 'An unexpected error occurred'));
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');

    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();
    await expect(page.getByText('In Use', { exact: true })).toHaveCount(0);
  });

  test('choosing a status asks the service for it', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await page.getByLabel('Status').selectOption('DRAFT');

    await expect.poll(() => lastListQuery(api).get('status')).toBe('DRAFT');
    expect(lastListQuery(api).get('page')).toBe('0');
  });

  test('sorting by a column asks the service for that order', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));

    await page.goto('/rubrics');
    await page.getByRole('button', { name: 'Updated' }).click();

    await expect.poll(() => lastListQuery(api).get('sort')).toBe('updatedAt,asc');
  });

  test('moving to the next page asks the service for it', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric], { totalElements: 45, totalPages: 3 }));

    await page.goto('/rubrics');
    await page.getByRole('button', { name: 'Next page' }).click();

    await expect.poll(() => lastListQuery(api).get('page')).toBe('1');
  });

  test('no rubrics shows an explanation instead of an empty table', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricPage([]));

    await page.goto('/rubrics');

    await expect(page.getByText('There are no rubrics yet.')).toBeVisible();
    await expect(page.getByRole('table')).toHaveCount(0);
  });

  test('a failed load says why and can be retried', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}`, rubricError(500, 'An unexpected error occurred'));

    await page.goto('/rubrics');

    await expect(page.getByRole('alert')).toContainText('The rubrics could not be loaded.');
    await expect(page.getByRole('alert')).toContainText('An unexpected error occurred');

    api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));
    await page.getByRole('button', { name: 'Try again' }).click();

    await expect(page.getByRole('row', { name: /Bare-Minimum/ })).toBeVisible();
    await expect(page.getByRole('alert')).toHaveCount(0);
  });
});

test.describe('rubric page', () => {
  test.beforeEach(async ({ api }) => {
    api.mock(`GET ${RUBRIC}`, rubricOk(bareMinimumRubric));
    api.mock(`GET ${RUBRIC}/events`, rubricOk(bareMinimumEvents));
  });

  test('lists the versions newest first and marks the active one', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements');

    await expect(page.getByRole('heading', { name: 'ACH Monthly Bare-Minimum Requirements' })).toBeVisible();
    await expect(page.getByText('Active version 1.1.0')).toBeVisible();

    const rows = page.getByRole('table', { name: 'Versions of this rubric' }).locator('tbody tr');
    await expect(rows).toHaveCount(3);
    await expect(rows.nth(0)).toContainText('1.2.0');
    await expect(rows.nth(0)).toContainText('Draft');
    await expect(rows.nth(0)).toContainText('Not run');
    await expect(rows.nth(0).locator('td.mat-column-checks')).toHaveText('5');
    await expect(rows.nth(1).locator('td.mat-column-checks')).toHaveText('4');
    await expect(rows.nth(1)).toContainText('Published');
    await expect(rows.nth(1)).toContainText('Active');
    await expect(rows.nth(1)).toContainText('Acceptable with warnings');
    await expect(rows.nth(2)).toContainText('Retired');
    await expect(rows.nth(2)).toContainText('Pass/fail');
    // Only the newest published version is the active one.
    await expect(page.getByText('Active', { exact: true })).toHaveCount(1);
  });

  test('says the rubric is In Use', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements');

    await expect(page.getByText('In Use', { exact: true })).toBeVisible();
  });

  test('a rubric the pipeline does not use is not marked', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/cross-measure-patient`, rubricOk({ ...draftOnlyRubric }));
    api.mock(`GET ${RUBRICS}/cross-measure-patient/events`, rubricOk([]));

    await page.goto('/rubrics/cross-measure-patient');

    await expect(page.getByRole('heading', { name: 'Cross-Measure Patient' })).toBeVisible();
    await expect(page.getByText('No published version')).toBeVisible();
    await expect(page.getByText('In Use', { exact: true })).toHaveCount(0);
  });

  test('the History tab shows who did what', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements');
    await page.getByRole('tab', { name: /History/ }).click();

    const rows = page.getByRole('table', { name: 'History of this rubric' }).locator('tbody tr');
    await expect(rows).toHaveCount(bareMinimumEvents.length);
    await expect(rows.nth(0)).toContainText('Saved as draft');
    await expect(rows.nth(0)).toContainText('author@example.org');
    await expect(rows.nth(1)).toContainText('Retired');
    await expect(rows.nth(2)).toContainText('Published');
    await expect(rows.nth(2)).toContainText('reviewer@example.org');
  });

  test('the versions are still shown when the history cannot be loaded', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}/events`, rubricError(500, 'An unexpected error occurred'));

    await page.goto('/rubrics/bare-minimum-requirements');

    await expect(page.getByRole('table', { name: 'Versions of this rubric' })).toBeVisible();
    await page.getByRole('tab', { name: /History/ }).click();
    await expect(page.getByRole('alert')).toContainText('The history could not be loaded.');
  });

  test('a version opens its own page', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk(publishedVersion));

    await page.goto('/rubrics/bare-minimum-requirements');
    await page.getByRole('link', { name: '1.1.0' }).click();

    await expect(page).toHaveURL(/\/rubrics\/bare-minimum-requirements\/versions\/1\.1\.0$/);
  });

  test('a rubric that does not exist says so', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/nope`, rubricError(404, 'Rubric not found: nope'));
    api.mock(`GET ${RUBRICS}/nope/events`, rubricError(404, 'Rubric not found: nope'));

    await page.goto('/rubrics/nope');

    await expect(page.getByRole('alert')).toContainText('There is no rubric with the id "nope".');
    await expect(page.getByRole('link', { name: 'Back to the rubrics' })).toBeVisible();
  });
});

test.describe('version page', () => {
  const VERSION_URL = '/rubrics/bare-minimum-requirements/versions/1.1.0';

  test.beforeEach(async ({ api }) => {
    api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk(publishedVersion));
  });

  test('shows the status, checksum and who saved and published it', async ({ page }) => {
    await page.goto(VERSION_URL);

    await expect(page.getByRole('heading', { name: /ACH Monthly Bare-Minimum Requirements · 1\.1\.0/ })).toBeVisible();
    await expect(page.getByText('Published', { exact: true }).first()).toBeVisible();
    await expect(page.getByText(publishedVersion.checksum)).toBeVisible();
    await expect(page.getByText('reviewer@example.org')).toBeVisible();
    await expect(page.getByText('Acceptable with warnings')).toBeVisible();
  });

  test('lists the checks in the order they run, with what each tests', async ({ page }) => {
    await page.goto(VERSION_URL);

    await expect(page.getByText('3 of 4 enabled')).toBeVisible();
    const rows = page.getByRole('table', { name: 'Checks of this version' }).locator('tbody tr.expandable-main-row');
    await expect(rows).toHaveCount(4);
    await expect(rows.nth(0)).toContainText('diagnosticreport-category-required');
    await expect(rows.nth(0)).toContainText('FHIRPath');
    await expect(rows.nth(0)).toContainText('DiagnosticReport.category.exists()');
    await expect(rows.nth(1)).toContainText('DiagnosticReport.category in http://hl7.org/fhir/ValueSet/diagnostic-service-sections|4.0.1');
    await expect(rows.nth(2)).toContainText('Any of 2 checks');
    await expect(rows.nth(3)).toContainText('Each resource against its own profile');
    await expect(rows.nth(3)).toContainText('No'); // disabled
  });

  test('selecting a check shows its settings, and its child checks when it is combined', async ({ page }) => {
    await page.goto(VERSION_URL);

    await page.getByRole('button', { name: 'diagnosticreport-category-required' }).click();
    await expect(page.getByText('Missing DiagnosticReport.category code.')).toBeVisible();

    await page.getByRole('button', { name: 'specimen-type-terminology' }).click();
    // Only one check is open at a time.
    await expect(page.getByText('Missing DiagnosticReport.category code.')).toHaveCount(0);
    await expect(page.getByText('Any of its checks passes')).toBeVisible();
    const children = page.getByRole('table', { name: 'Child checks' }).locator('tbody tr');
    await expect(children).toHaveCount(2);
    await expect(children.nth(0)).toContainText('not-lab');
    await expect(children.nth(1)).toContainText('DiagnosticReport.specimen.type in');
  });

  test('the Settings tab shows the scoring and what the version applies to', async ({ page }) => {
    await page.goto(VERSION_URL);
    await page.getByRole('tab', { name: 'Settings' }).click();

    await expect(page.getByText('Dimension scorecard, worst of')).toBeVisible();
    await expect(page.getByText('Conformance, Terminology')).toBeVisible();
    await expect(page.getByText('DiagnosticReport, Patient')).toBeVisible();
    await expect(page.getByText('nhsn-dqm')).toBeVisible();
  });

  test('the Definition tab shows the definition as JSON', async ({ page }) => {
    await page.goto(VERSION_URL);
    await page.getByRole('tab', { name: 'Definition' }).click();

    const shown = await page.getByLabel('Definition as JSON').textContent();
    expect(JSON.parse(shown ?? '')).toEqual(publishedVersion.definition);
  });

  for (const format of [
    { button: 'Export YAML', accept: 'application/yaml', file: 'bare-minimum-requirements-1.1.0.yaml', body: 'id: bare-minimum-requirements\nsemver: 1.1.0\n' },
    { button: 'Export JSON', accept: 'application/json', file: 'bare-minimum-requirements-1.1.0.json', body: '{\n  "id" : "bare-minimum-requirements"\n}' },
  ]) {
    test(`${format.button} downloads the file the service writes, under the rubric and version name`, async ({ page, api }) => {
      api.mock(`GET ${RUBRIC}/versions/1.1.0/definition`, { status: 200, body: format.body, contentType: format.accept });

      await page.goto(VERSION_URL);
      const downloading = page.waitForEvent('download');
      await page.getByRole('button', { name: format.button }).click();
      const download = await downloading;

      expect(download.suggestedFilename()).toBe(format.file);
      const stream = await download.createReadStream();
      const chunks: Buffer[] = [];
      for await (const chunk of stream) chunks.push(chunk as Buffer);
      // exactly what the service sent: the file is not rebuilt in the browser
      expect(Buffer.concat(chunks).toString('utf-8')).toBe(format.body);

      const requests = api.callsTo('GET', `${RUBRIC}/versions/1.1.0/definition`);
      expect(requests).toHaveLength(1);
    });
  }

  test('the export asks for the format in the Accept header', async ({ page, api }) => {
    const accepts: (string | undefined)[] = [];
    api.mock(`GET ${RUBRIC}/versions/1.1.0/definition`, async (route) => {
      accepts.push(route.request().headers()['accept']);
      await route.fulfill({ status: 200, body: 'id: x\n', contentType: 'application/yaml' });
    });

    await page.goto(VERSION_URL);
    const downloading = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Export YAML' }).click();
    await downloading;

    expect(accepts).toEqual(['application/yaml']);
  });

  test('a failed export says why and downloads nothing', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}/versions/1.1.0/definition`, rubricError(500, 'An unexpected error occurred'));
    let downloads = 0;
    page.on('download', () => downloads++);

    await page.goto(VERSION_URL);
    await page.getByRole('button', { name: 'Export YAML' }).click();

    await expect(page.getByRole('alert')).toContainText('The definition could not be exported.');
    await expect(page.getByRole('alert')).toContainText('An unexpected error occurred');
    await expect(page.getByRole('button', { name: 'Export YAML' })).toBeEnabled();
    expect(downloads).toBe(0);
  });

  test('a version that does not exist says so and links back to the rubric', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}/versions/9.9.9`, rubricError(404, 'Rubric version not found'));

    await page.goto('/rubrics/bare-minimum-requirements/versions/9.9.9');

    await expect(page.getByRole('alert')).toContainText('has no version 9.9.9');
    await expect(page.getByRole('link', { name: 'Back to the rubric' })).toBeVisible();
  });
});
