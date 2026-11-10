import { Route } from '@playwright/test';
import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import {
  bareMinimumEvents,
  bareMinimumRubric,
  draftVersion,
  dryRunResult,
  lifecycleResult,
  publishedVersion,
  rubricError,
  rubricOk,
  rubricOwners,
  rubricPage,
  rubricSettings,
} from '../fixtures/rubrics';

const RUBRICS = '/api/validation/rubrics';
const RUBRIC = `${RUBRICS}/bare-minimum-requirements`;
const DRY_RUN = '/api/validation/v2/rubrics/bare-minimum-requirements/versions/1.2.0/$dry-run';
const RUBRIC_URL = '/rubrics/bare-minimum-requirements';
const DRAFT_URL = `${RUBRIC_URL}/versions/1.2.0`;
const ACTIVE_URL = `${RUBRIC_URL}/versions/1.1.0`;

const SAMPLE = { resourceType: 'Bundle', type: 'collection', entry: [{ resource: { resourceType: 'Patient', id: 'p1' } }] };

/** Answers an endpoint, keeps what was sent to it, and runs `then` once it has answered. */
function capture(api: ApiMock, key: string, answer: object, then?: () => void): { body: any }[] {
  const sent: { body: any }[] = [];
  api.mock(key, async (route: Route) => {
    const text = route.request().postData();
    sent.push({ body: text ? JSON.parse(text) : null });
    const { status, json } = answer as { status: number; json: object };
    await route.fulfill({ status, json });
    then?.();
  });
  return sent;
}

test.beforeEach(async ({ api }) => {
  api.mock(`GET ${RUBRICS}/$owners`, rubricOk(rubricOwners));
  api.mock(`GET ${RUBRICS}/$settings`, rubricOk(rubricSettings));
  api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));
  api.mock(`GET ${RUBRIC}`, rubricOk(bareMinimumRubric));
  api.mock(`GET ${RUBRIC}/events`, rubricOk(bareMinimumEvents));
  api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk(publishedVersion));
  api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk(draftVersion));
});

test.describe('dry run', () => {
  test('from a draft: paste a sample, run it, and read what it found', async ({ page, api }) => {
    const runs = capture(api, `POST ${DRY_RUN}`, rubricOk(dryRunResult));

    await page.goto(DRAFT_URL);
    await page.getByRole('link', { name: 'Dry run', exact: true }).click();
    await expect(page.getByRole('heading', { name: /Dry run · ACH Monthly Bare-Minimum Requirements · 1\.2\.0/ })).toBeVisible();

    await expect(page.getByRole('button', { name: 'Run', exact: true })).toBeDisabled();
    await page.getByLabel('Or paste the sample').fill(JSON.stringify(SAMPLE));
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    const result = page.getByRole('region', { name: 'Result' });
    await expect(result).toContainText('Unacceptable');
    await expect(result).toContainText('2 errors');
    await expect(result).toContainText('1 warning');
    await expect(result).toContainText('840 ms');
    await expect(result).toContainText('Bundle with 1 entry');
    await expect(result).toContainText('Conformance: Unacceptable');
    await expect(result).toContainText('The dry run completed and is recorded on version 1.2.0.');

    expect(runs).toHaveLength(1);
    expect(runs[0].body).toEqual({ payload: SAMPLE });

    const findings = page.getByRole('table', { name: 'Findings of this dry run' });
    await expect(findings.getByRole('row')).toHaveCount(4);
    await expect(findings).toContainText('Bundle.entry[2].resource');
  });

  test('the findings can be narrowed, and a check leads to its own', async ({ page, api }) => {
    api.mock(`POST ${DRY_RUN}`, rubricOk(dryRunResult));
    await page.goto(`${DRAFT_URL}/dry-run`);
    await page.getByLabel('Or paste the sample').fill(JSON.stringify(SAMPLE));
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    const findings = page.getByRole('table', { name: 'Findings of this dry run' });
    await page.getByLabel('Severity').selectOption('WARNING');
    await expect(findings.getByRole('row')).toHaveCount(2);
    await expect(page.getByText('Showing 1 of 1 matching findings.')).toBeVisible();

    await page.getByLabel('Severity').selectOption('INFORMATION');
    await expect(page.getByText('No finding matches these filters.')).toBeVisible();

    await page.getByRole('button', { name: 'Clear filters' }).click();
    await expect(findings.getByRole('row')).toHaveCount(4);

    const checks = page.getByRole('table', { name: 'Checks of this dry run' });
    // the four checks that ran, and the disabled one, which did not
    await expect(checks.getByRole('row')).toHaveCount(6);
    await expect(checks).toContainText('Disabled, not run');
    await checks.getByRole('button', { name: 'Show the 2 findings of diagnosticreport-category-required' }).click();
    await expect(page.getByLabel('Check', { exact: true })).toHaveValue('diagnosticreport-category-required');
    await expect(findings.getByRole('row')).toHaveCount(3);
  });

  test('a file can be uploaded instead, and is sent as it is', async ({ page, api }) => {
    const runs = capture(api, `POST ${DRY_RUN}`, rubricOk(dryRunResult));
    await page.goto(`${DRAFT_URL}/dry-run`);

    await page.locator('#dryRunFile').setInputFiles({
      name: 'patient-bundle.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(SAMPLE)),
    });
    await expect(page.getByText('patient-bundle.json')).toBeVisible();
    await expect(page.getByLabel('Or paste the sample')).toHaveCount(0);
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    await expect(page.getByRole('region', { name: 'Result' })).toContainText('patient-bundle.json: Bundle with 1 entry');
    expect(runs[0].body).toEqual({ payload: SAMPLE });
  });

  test('a sample that is not FHIR JSON is not sent', async ({ page, api }) => {
    await page.goto(`${DRAFT_URL}/dry-run`);

    await page.getByLabel('Or paste the sample').fill('<Bundle xmlns="http://hl7.org/fhir"/>');
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    await expect(page.getByText('The sample looks like XML. A dry run takes FHIR as JSON.')).toBeVisible();
    expect(api.callsTo('POST', DRY_RUN)).toHaveLength(0);
  });

  test('a run the service refuses says why and can be tried again', async ({ page, api }) => {
    api.mock(`POST ${DRY_RUN}`, rubricError(400, 'Failed to parse FHIR payload: Unknown resource name "Bundel"'));
    await page.goto(`${DRAFT_URL}/dry-run`);
    await page.getByLabel('Or paste the sample').fill('{"resourceType":"Bundel"}');
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    await expect(page.getByText('The dry run could not be completed.')).toBeVisible();
    await expect(page.getByText('Unknown resource name "Bundel"')).toBeVisible();

    api.mock(`POST ${DRY_RUN}`, rubricOk(dryRunResult));
    await page.getByLabel('Or paste the sample').fill(JSON.stringify(SAMPLE));
    await page.getByRole('button', { name: 'Run', exact: true }).click();
    await expect(page.getByRole('region', { name: 'Result' })).toBeVisible();
    await expect(page.getByText('The dry run could not be completed.')).toHaveCount(0);
  });

  test('after a dry run a draft can go straight to publishing', async ({ page, api }) => {
    api.mock(`POST ${DRY_RUN}`, rubricOk(dryRunResult));
    await page.goto(`${DRAFT_URL}/dry-run`);
    await page.getByLabel('Or paste the sample').fill(JSON.stringify(SAMPLE));
    await page.getByRole('button', { name: 'Run', exact: true }).click();

    await page.getByRole('region', { name: 'Result' }).getByRole('link', { name: 'Publish' }).click();

    await expect(page.getByRole('region', { name: 'Publish version 1.2.0?' })).toBeVisible();
  });
});

test.describe('publishing', () => {
  test('from the rubric page: the confirmation says what changes, and confirming publishes', async ({ page, api }) => {
    const published = capture(api, `POST ${RUBRIC}/versions/1.2.0/$publish`, lifecycleResult('1.2.0', 'PUBLISHED'), () => {
      api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk({ ...draftVersion, status: 'PUBLISHED', publishedAt: '2026-09-08T10:15:30Z' }));
      api.mock(`GET ${RUBRIC}`, rubricOk({ ...bareMinimumRubric, latestPublishedSemver: '1.2.0' }));
    });

    await page.goto(RUBRIC_URL);
    await page.getByRole('link', { name: 'Publish version 1.2.0' }).click();

    const confirmation = page.getByRole('region', { name: 'Publish version 1.2.0?' });
    await expect(confirmation).toContainText('5 checks, 4 enabled');
    await expect(confirmation).toContainText('1.2.0 becomes the active version of this rubric');
    await expect(confirmation).toContainText('1.1.0 stays published but is no longer used.');
    await expect(confirmation).toContainText('A published version can never be edited.');
    // the pipeline compares against this rubric, and this draft has had no dry run
    await expect(confirmation).toContainText('Once published, this version is the one compared.');
    await expect(confirmation).toContainText('This draft has not been dry-run since it was last saved. It can still be published.');
    expect(published).toHaveLength(0);

    await confirmation.getByRole('button', { name: 'Publish' }).click();

    await expect(page.getByText('Version 1.2.0 is published and is now the active version.')).toBeVisible();
    await expect(confirmation).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Retire' })).toBeVisible();
    expect(published).toHaveLength(1);
    await expect(page).toHaveURL(new RegExp(`${DRAFT_URL.replace(/\./g, '\\.')}$`));
  });

  test('cancelling leaves the draft a draft', async ({ page, api }) => {
    await page.goto(DRAFT_URL);
    await page.getByRole('button', { name: 'Publish' }).click();

    await page.getByRole('region', { name: 'Publish version 1.2.0?' }).getByRole('button', { name: 'Cancel' }).click();

    await expect(page.getByRole('region', { name: 'Publish version 1.2.0?' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible();
    expect(api.callsTo('POST', `${RUBRIC}/versions/1.2.0/$publish`)).toHaveLength(0);
  });

  test('where a dry run is required and there is none, it cannot be confirmed and offers one', async ({ page, api }) => {
    api.mock(`GET ${RUBRICS}/$settings`, rubricOk({ ...rubricSettings, dryRunRequiredForPublish: true }));

    await page.goto(`${DRAFT_URL}?do=publish`);

    const confirmation = page.getByRole('region', { name: 'Publish version 1.2.0?' });
    await expect(confirmation).toContainText('This environment requires a dry run before publishing');
    await expect(confirmation.getByRole('button', { name: 'Publish' })).toHaveCount(0);

    await confirmation.getByRole('link', { name: 'Run a dry run' }).click();
    await expect(page).toHaveURL(/\/versions\/1\.2\.0\/dry-run$/);
  });

  test('a refusal from the service is shown, and the confirmation stays open', async ({ page, api }) => {
    api.mock(`POST ${RUBRIC}/versions/1.2.0/$publish`,
      rubricError(409, 'Cannot publish bare-minimum-requirements v1.2.0: no dry run has been completed for this version'));

    await page.goto(`${DRAFT_URL}?do=publish`);
    const confirmation = page.getByRole('region', { name: 'Publish version 1.2.0?' });
    await confirmation.getByRole('button', { name: 'Publish' }).click();

    await expect(confirmation).toContainText('no dry run has been completed for this version');
    await expect(confirmation.getByRole('button', { name: 'Publish' })).toBeEnabled();
  });
});

test.describe('retiring', () => {
  test('the last published version of the rubric the pipeline uses needs the rubric id typed', async ({ page, api }) => {
    const retired = capture(api, `POST ${RUBRIC}/versions/1.1.0/$retire`, lifecycleResult('1.1.0', 'RETIRED'), () => {
      api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk({ ...publishedVersion, status: 'RETIRED', retiredAt: '2026-09-08T10:15:30Z' }));
      api.mock(`GET ${RUBRIC}`, rubricOk({ ...bareMinimumRubric, latestPublishedSemver: undefined }));
    });

    await page.goto(RUBRIC_URL);
    await page.getByRole('link', { name: 'Retire version 1.1.0' }).click();

    const confirmation = page.getByRole('region', { name: 'Retire version 1.1.0?' });
    await expect(confirmation).toContainText('No published version will remain');
    await expect(confirmation).toContainText('With no published version, pipeline validation has nothing to run.');
    const retire = confirmation.getByRole('button', { name: 'Retire' });
    await expect(retire).toBeDisabled();

    await confirmation.getByLabel(/Type the rubric's id/).fill('bare-minimum');
    await expect(retire).toBeDisabled();
    await confirmation.getByLabel(/Type the rubric's id/).fill('bare-minimum-requirements');
    await retire.click();

    await expect(page.getByText('Version 1.1.0 is retired.')).toBeVisible();
    expect(retired).toHaveLength(1);
    await expect(page.getByRole('button', { name: 'Retire' })).toHaveCount(0);
  });

  test('says which version becomes active again when another is published', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}`, rubricOk({
      ...bareMinimumRubric,
      versions: bareMinimumRubric.versions!.map(version => version.semver === '1.0.0'
        ? { ...version, status: 'PUBLISHED', retiredAt: undefined, retiredBy: undefined }
        : version),
    }));

    await page.goto(`${ACTIVE_URL}?do=retire`);

    const confirmation = page.getByRole('region', { name: 'Retire version 1.1.0?' });
    await expect(confirmation).toContainText('1.0.0 becomes the active version again.');
    await expect(confirmation.getByRole('button', { name: 'Retire' })).toBeEnabled();
    await expect(confirmation.getByLabel(/Type the rubric's id/)).toHaveCount(0);
  });
});

test.describe('deleting a draft', () => {
  test('is confirmed, then goes back to the rubric', async ({ page, api }) => {
    let deleted = 0;
    api.mock(`DELETE ${RUBRIC}/versions/1.2.0`, async (route: Route) => {
      deleted++;
      await route.fulfill({ status: 204 });
    });

    await page.goto(RUBRIC_URL);
    await page.getByRole('link', { name: 'Delete version 1.2.0' }).click();

    const confirmation = page.getByRole('region', { name: 'Delete draft 1.2.0?' });
    await expect(confirmation).toContainText('The draft and its checks are removed. This cannot be undone.');
    await expect(confirmation).toContainText('the deletion is recorded in the rubric\'s history');
    expect(deleted).toBe(0);

    await confirmation.getByRole('button', { name: 'Delete' }).click();

    await expect(page).toHaveURL(new RegExp(`${RUBRIC_URL}$`));
    expect(deleted).toBe(1);
  });

  test('a published version offers no delete', async ({ page }) => {
    await page.goto(ACTIVE_URL);

    await expect(page.getByRole('button', { name: 'Retire' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Delete' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Publish' })).toHaveCount(0);
  });
});

test.describe('comparing versions', () => {
  test('from a draft: what it adds to the active version', async ({ page }) => {
    await page.goto(DRAFT_URL);
    await page.getByRole('link', { name: 'Compare with 1.1.0' }).click();

    await expect(page).toHaveURL(/\/compare\?from=1\.1\.0&to=1\.2\.0$/);
    await expect(page.getByText('0 settings changed · 1 check added · 0 removed · 0 changed · 4 unchanged')).toBeVisible();
    const added = page.getByRole('table', { name: 'Checks added' });
    await expect(added).toContainText('patient-name-present');
    await expect(added).toContainText('Patient.name.exists()');
  });

  test('from the rubric page it chooses the newest against the active version, and the sides can be swapped', async ({ page }) => {
    await page.goto(RUBRIC_URL);
    await page.getByRole('link', { name: 'Compare versions' }).click();

    await expect(page).toHaveURL(/\/compare\?from=1\.1\.0&to=1\.2\.0$/);
    await expect(page.getByLabel('From')).toHaveValue('1.1.0');
    await expect(page.getByLabel('To', { exact: true })).toHaveValue('1.2.0');

    await page.getByRole('button', { name: 'Swap the two versions' }).click();

    await expect(page).toHaveURL(/\/compare\?from=1\.2\.0&to=1\.1\.0$/);
    await expect(page.getByRole('table', { name: 'Checks removed' })).toContainText('patient-name-present');
  });

  test('a changed check lists what changed in it, side by side', async ({ page, api }) => {
    api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk({
      ...draftVersion,
      definition: {
        ...draftVersion.definition!,
        title: 'ACH Monthly Bare-Minimum Requirements, revised',
        checks: draftVersion.definition!.checks.map(check => check.id === 'diagnosticreport-category-required'
          ? { ...check, severityOverride: 'WARNING', parameters: { ...check.parameters, expression: 'DiagnosticReport.category.count() = 1' } }
          : check),
      },
    }));

    await page.goto(`${RUBRIC_URL}/compare?from=1.1.0&to=1.2.0`);

    await expect(page.getByRole('table', { name: 'Settings changed' })).toContainText('ACH Monthly Bare-Minimum Requirements, revised');
    const changed = page.getByRole('table', { name: 'Checks changed' });
    await expect(changed).toContainText('diagnosticreport-category-required');
    await expect(changed).toContainText('Severity');
    await expect(changed).toContainText('DiagnosticReport.category.exists()');
    await expect(changed).toContainText('DiagnosticReport.category.count() = 1');
  });

  test('choosing the same version twice asks for two different ones', async ({ page }) => {
    await page.goto(`${RUBRIC_URL}/compare?from=1.1.0&to=1.2.0`);

    await page.getByLabel('From').selectOption('1.2.0');

    await expect(page.getByText('Choose two different versions.')).toBeVisible();
  });
});
