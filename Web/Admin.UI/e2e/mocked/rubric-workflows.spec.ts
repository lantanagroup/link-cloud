import { Route } from '@playwright/test';
import { test, expect } from '../support/test';
import {
  bareMinimumEvents,
  bareMinimumRubric,
  draftVersion,
  dryRunResult,
  lifecycleResult,
  publishedDefinition,
  publishedVersion,
  rubricOk,
  rubricOwners,
  rubricPage,
  rubricSettings,
  savedDraft,
} from '../fixtures/rubrics';
import { IRubricDefinition, IRubricVersionSummary } from '../../src/app/interfaces/validation/rubric.interface';

const RUBRICS = '/api/validation/rubrics';
const RUBRIC = `${RUBRICS}/bare-minimum-requirements`;
const RUBRIC_URL = '/rubrics/bare-minimum-requirements';

// The other rubric specs each test one page against fixed answers. These follow one piece of work across
// the pages, with the answers changing as the work is done, the way the service's would.

test.beforeEach(async ({ api }) => {
  api.mock(`GET ${RUBRICS}/$owners`, rubricOk(rubricOwners));
  api.mock(`GET ${RUBRICS}/$settings`, rubricOk(rubricSettings));
  api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));
  api.mock(`GET ${RUBRIC}`, rubricOk(bareMinimumRubric));
  api.mock(`GET ${RUBRIC}/events`, rubricOk(bareMinimumEvents));
  api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk(publishedVersion));
  api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk(draftVersion));
});

test('changing a published rubric: new version, edit, save, dry run, compare, publish', async ({ page, api }) => {
  const done: string[] = [];
  let definition: IRubricDefinition = { ...publishedDefinition, semver: '1.2.1' };
  const summary: IRubricVersionSummary = {
    rubricVersionId: 30, rubricId: 'bare-minimum-requirements', semver: '1.2.1', status: 'DRAFT', checksum: 'c0ffee', checkCount: 4,
  };

  /** Answers for the new version as it stands: the version itself, and the rubric that lists it. */
  const serve = (changes: Partial<IRubricVersionSummary> = {}): void => {
    Object.assign(summary, changes);
    api.mock(`GET ${RUBRIC}/versions/1.2.1`, rubricOk({ ...draftVersion, ...summary, definition }));
    api.mock(`GET ${RUBRIC}`, rubricOk({
      ...bareMinimumRubric,
      latestPublishedSemver: summary.status === 'PUBLISHED' ? '1.2.1' : '1.1.0',
      versions: [summary, ...bareMinimumRubric.versions!],
    }));
  };

  api.mock(`POST ${RUBRIC}/versions`, async (route: Route) => {
    done.push('save');
    definition = JSON.parse(route.request().postData() ?? '{}');
    serve();
    const { status, json } = savedDraft('bare-minimum-requirements', '1.2.1');
    await route.fulfill({ status, json });
  });
  api.mock('POST /api/validation/v2/rubrics/bare-minimum-requirements/versions/1.2.1/$dry-run', async (route: Route) => {
    done.push('dry run');
    serve({ dryRunCompletedAt: '2026-09-08T10:15:30Z', dryRunStatus: 'UNACCEPTABLE' });
    await route.fulfill({ status: 200, json: rubricOk({ ...dryRunResult, rubricVersion: '1.2.1' }).json });
  });
  api.mock(`POST ${RUBRIC}/versions/1.2.1/$publish`, async (route: Route) => {
    done.push('publish');
    serve({ status: 'PUBLISHED', publishedAt: '2026-09-08T10:20:00Z' });
    await route.fulfill({ status: 200, json: lifecycleResult('1.2.1', 'PUBLISHED').json });
  });

  // New version from the active one, with one change.
  await page.goto(`${RUBRIC_URL}/versions/1.1.0`);
  await page.getByRole('link', { name: 'New version from this' }).click();
  await expect(page.getByRole('heading', { name: 'New version of bare-minimum-requirements' })).toBeVisible();
  await expect(page.getByLabel('Version', { exact: true })).toHaveValue('1.2.1');
  await page.getByLabel('Owner').fill('lantana');
  await page.getByRole('button', { name: 'Save draft' }).click();
  await expect(page).toHaveURL(/\/versions\/1\.2\.1\/edit$/);
  await expect(page.getByText('All changes saved')).toBeVisible();

  // The saved draft, on the rubric page: not yet dry-run, and the old version still the active one.
  await page.goto(RUBRIC_URL);
  const versions = page.getByRole('table', { name: 'Versions of this rubric' });
  await expect(versions.getByRole('row', { name: /1\.2\.1/ })).toContainText('Not run');
  await expect(versions.getByRole('row', { name: /1\.1\.0/ })).toContainText('Active');

  // Dry run.
  await page.getByRole('link', { name: 'Dry run version 1.2.1' }).click();
  await page.getByLabel('Or paste the sample').fill('{"resourceType":"Bundle","entry":[{}]}');
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Result' })).toContainText('recorded on version 1.2.1');

  // Compare with the active version: the one change, and nothing else.
  await page.getByRole('link', { name: 'Back to the version' }).click();
  await page.getByRole('link', { name: 'Compare with 1.1.0' }).click();
  await expect(page.getByText('1 setting changed · 0 checks added · 0 removed · 0 changed · 4 unchanged')).toBeVisible();
  const settings = page.getByRole('table', { name: 'Settings changed' });
  await expect(settings).toContainText('Owner');
  await expect(settings).toContainText('lantana');

  // Publish. The dry run is on the version, so the confirmation has no warning about one.
  await page.getByRole('link', { name: '1.2.1', exact: true }).click();
  await page.getByRole('button', { name: 'Publish' }).click();
  const confirmation = page.getByRole('region', { name: 'Publish version 1.2.1?' });
  await expect(confirmation).toContainText('Dry run: Unacceptable');
  await expect(confirmation).toContainText('1.1.0 stays published but is no longer used.');
  await expect(confirmation).not.toContainText('has not been dry-run');
  await confirmation.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText('Version 1.2.1 is published and is now the active version.')).toBeVisible();

  // The rubric page agrees: the new version is the active one.
  await page.goto(RUBRIC_URL);
  await expect(versions.getByRole('row', { name: /1\.2\.1/ })).toContainText('Active');
  await expect(versions.getByRole('row', { name: /1\.1\.0/ })).not.toContainText('Active');

  expect(done).toEqual(['save', 'dry run', 'publish']);
});

test('moving a rubric between environments: what is exported is what import accepts', async ({ page, api }) => {
  const exported = JSON.stringify(publishedDefinition, null, 2);
  api.mock(`GET ${RUBRIC}/versions/1.1.0/definition`, { status: 200, body: exported, contentType: 'application/json' });
  let reviewed = '';
  api.mock(`POST ${RUBRICS}/$validate`, async (route: Route) => {
    reviewed = route.request().postData() ?? '';
    // the other environment has no rubric with this id
    await route.fulfill({
      status: 200,
      json: rubricOk({ valid: true, errors: [], definition: publishedDefinition, checksum: publishedVersion.checksum, rubricExists: false }).json,
    });
  });

  await page.goto(`${RUBRIC_URL}/versions/1.1.0`);
  const download = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Export JSON' }).click();
  const file = await download;
  expect(file.suggestedFilename()).toBe('bare-minimum-requirements-1.1.0.json');
  const text = (await (await file.createReadStream()).toArray()).join('');

  await page.goto('/rubrics/-/import');
  await page.locator('#importFile').setInputFiles({ name: file.suggestedFilename(), mimeType: 'application/json', buffer: Buffer.from(text) });
  await page.getByRole('button', { name: 'Review' }).click();

  await expect(page.getByText('Importing creates the rubric')).toBeVisible();
  await expect(page.getByText('4 (3 enabled)')).toBeVisible();
  expect(JSON.parse(reviewed)).toEqual(publishedDefinition);
});
