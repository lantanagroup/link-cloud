import { test, expect } from '@playwright/test';
import { assertBackendReachable, watchApiFailures } from '../support/live';

// Not run in CI for now. The live tier runs inside the job that gates merges, and the first test leaves a
// retired rubric behind on every run, because a version that was published cannot be deleted. Run it by
// hand against a local stack (npm run e2e:live) until there is a way to remove what it leaves.
test.skip(!!process.env['CI'], 'Not run in CI: it leaves a retired rubric behind each run');

test.beforeAll(async ({ request }) => {
  await assertBackendReachable(request);
});

// Live tier: the rubric screens against the real Validation service, through the BFF. The mocked specs
// answer every rubric call from fixtures, so they cannot tell when the service's answers change shape;
// these two paths can.
//
// Each test makes a rubric of its own, named by the time, and touches no other. The rubric uses FHIRPath
// checks only, so it needs no terminology service.

/** A definition with one check that the sample below fails and one it passes. */
function definition(rubricId: string): string {
  return [
    `id: ${rubricId}`,
    'semver: 1.0.0',
    'title: UI smoke test rubric',
    'owner: ui-smoke',
    'dimensions:',
    '- CONFORMANCE',
    'scoringPolicy:',
    '  type: piqi-pass-fail',
    'checks:',
    '- id: patient-birthdate-required',
    '  type: FHIRPATH',
    '  dimension: CONFORMANCE',
    '  severityOverride: ERROR',
    '  enabled: true',
    '  parameters:',
    '    expression: Patient.birthDate.exists()',
    '    failureMessage: Missing Patient.birthDate.',
    '- id: patient-id-required',
    '  type: FHIRPATH',
    '  dimension: CONFORMANCE',
    '  severityOverride: ERROR',
    '  enabled: true',
    '  parameters:',
    '    expression: Patient.id.exists()',
    '',
  ].join('\n');
}

const SAMPLE = JSON.stringify({
  resourceType: 'Bundle',
  type: 'collection',
  entry: [{ resource: { resourceType: 'Patient', id: 'p1' } }],
});

/** Imports the definition as a draft through the Import page and lands on the draft's own page. */
async function importDraft(page: import('@playwright/test').Page, rubricId: string): Promise<void> {
  await page.goto('/rubrics/-/import');
  await page.getByLabel('Definition, as YAML or JSON').fill(definition(rubricId));
  await page.getByRole('button', { name: 'Review' }).click();
  await expect(page.getByText('Importing creates the rubric')).toBeVisible({ timeout: 30_000 });
  await page.getByRole('button', { name: 'Import as a draft' }).click();
  await page.waitForURL(`**/rubrics/${rubricId}/versions/1.0.0`);
  await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible();
}

test('a rubric is imported, dry-run, published and retired', async ({ page }) => {
  const failures = watchApiFailures(page);
  const rubricId = `ui-smoke-${Date.now()}`;

  await importDraft(page, rubricId);

  // Dry run: the sample has no birth date, so the first check finds it and the second finds nothing.
  await page.getByRole('link', { name: 'Dry run', exact: true }).click();
  await page.getByLabel('Or paste the sample').fill(SAMPLE);
  await page.getByRole('button', { name: 'Run', exact: true }).click();
  const result = page.getByRole('region', { name: 'Result' });
  await expect(result).toContainText('Unacceptable', { timeout: 30_000 });
  await expect(result).toContainText('1 error');
  const findings = page.getByRole('table', { name: 'Findings of this dry run' });
  await expect(findings).toContainText('patient-birthdate-required');
  await expect(findings).toContainText('Missing Patient.birthDate.');
  await expect(page.getByRole('table', { name: 'Checks of this dry run' })).toContainText('patient-id-required');

  // Publish, straight from the result. The dry run is on the version, so nothing warns of a missing one.
  await result.getByRole('link', { name: 'Publish' }).click();
  const publish = page.getByRole('region', { name: 'Publish version 1.0.0?' });
  await expect(publish).toContainText('Dry run: Unacceptable');
  await expect(publish).not.toContainText('has not been dry-run');
  await publish.getByRole('button', { name: 'Publish' }).click();
  await expect(page.getByText('Version 1.0.0 is published and is now the active version.')).toBeVisible();
  await expect(page.getByText('Active', { exact: true })).toBeVisible();

  // Retire. It is the rubric's only published version, and not the pipeline's rubric, so a click is enough.
  await page.getByRole('button', { name: 'Retire' }).click();
  const retire = page.getByRole('region', { name: 'Retire version 1.0.0?' });
  await expect(retire).toContainText('No published version will remain');
  await retire.getByRole('button', { name: 'Retire' }).click();
  await expect(page.getByText('Version 1.0.0 is retired.')).toBeVisible();

  // The history has all three, newest first.
  await page.goto(`/rubrics/${rubricId}`);
  await page.getByRole('tab', { name: /History/ }).click();
  const history = page.getByRole('table', { name: 'History of this rubric' });
  await expect(history.getByRole('row')).toHaveCount(4);
  await expect(history).toContainText('Retired');
  await expect(history).toContainText('Published');
  await expect(history).toContainText('Saved as draft');

  expect(failures).toEqual([]);
  // What is left behind is one retired rubric nothing uses: a version that was published cannot be deleted.
});

test('a draft that was never published is deleted, and its rubric with it', async ({ page, request }) => {
  const failures = watchApiFailures(page);
  const rubricId = `ui-smoke-${Date.now()}-d`;

  await importDraft(page, rubricId);

  await page.getByRole('button', { name: 'Delete' }).click();
  const confirmation = page.getByRole('region', { name: 'Delete draft 1.0.0?' });
  await expect(confirmation).toContainText('the rubric is removed with it');
  await confirmation.getByRole('button', { name: 'Delete' }).click();

  // Its only version is gone, so the rubric is too, and there is nothing to go back to but the list.
  await page.waitForURL('**/rubrics');
  expect(failures).toEqual([]);

  await page.goto(`/rubrics/${rubricId}`);
  await expect(page.getByRole('alert')).toContainText(`There is no rubric with the id "${rubricId}".`);
});
