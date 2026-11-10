import { Page, Route } from '@playwright/test';
import { test, expect } from '../support/test';
import { ApiMock } from '../support/api-mock';
import {
  bareMinimumRubric,
  draftDefinition,
  draftVersion,
  publishedDefinition,
  publishedVersion,
  rubricError,
  rubricOk,
  rubricOwners,
  rubricPage,
  rubricSettings,
  savedDraft,
  validationResult,
} from '../fixtures/rubrics';

const RUBRICS = '/api/validation/rubrics';
const RUBRIC = `${RUBRICS}/bare-minimum-requirements`;

/** A save or a validate as the service received it. */
interface Sent {
  url: string;
  headers: Record<string, string>;
  body: any;
}

/** Answers an endpoint and keeps what was sent to it, headers included. */
function capture(api: ApiMock, key: string, answer: object): Sent[] {
  const sent: Sent[] = [];
  api.mock(key, async (route: Route) => {
    const request = route.request();
    const text = request.postData() ?? '';
    let body: any = text;
    try { body = JSON.parse(text); } catch { /* YAML is kept as text */ }
    sent.push({ url: request.url(), headers: request.headers(), body });
    const { status, json } = answer as { status: number; json: object };
    await route.fulfill({ status, json });
  });
  return sent;
}

/** Answers "leave without saving?" for the rest of the test, and counts how often it was asked. */
function answerLeavePrompts(page: Page, leave: boolean): { asked: number } {
  const prompts = { asked: 0 };
  page.on('dialog', async (dialog) => {
    prompts.asked++;
    if (leave) await dialog.accept(); else await dialog.dismiss();
  });
  return prompts;
}

test.beforeEach(async ({ api }) => {
  api.mock(`GET ${RUBRICS}/$owners`, rubricOk(rubricOwners));
  api.mock(`GET ${RUBRICS}/$settings`, rubricOk(rubricSettings));
  api.mock(`GET ${RUBRICS}`, rubricPage([bareMinimumRubric]));
  api.mock(`GET ${RUBRIC}`, rubricOk(bareMinimumRubric));
  api.mock(`GET ${RUBRIC}/events`, rubricOk([]));
  api.mock(`GET ${RUBRIC}/versions/1.1.0`, rubricOk(publishedVersion));
  api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk(draftVersion));
});

test.describe('creating a rubric', () => {
  test('from the list: fill in the details, add a check, validate, save', async ({ page, api }) => {
    const validated = capture(api, `POST ${RUBRICS}/$validate`, validationResult({ valid: true }));
    const saved = capture(api, `POST ${RUBRICS}/lab.results/versions`, savedDraft('lab.results', '1.0.0'));
    api.mock(`GET ${RUBRICS}/lab.results/versions/1.0.0`, rubricOk({
      ...draftVersion, rubricId: 'lab.results', semver: '1.0.0',
      definition: { ...publishedDefinition, id: 'lab.results', semver: '1.0.0', checks: [] },
    }));
    api.mock(`GET ${RUBRICS}/lab.results`, rubricOk({ rubricId: 'lab.results', versions: [] }));

    await page.goto('/rubrics');
    await page.getByRole('link', { name: 'New rubric' }).click();
    await expect(page.getByRole('heading', { name: 'New rubric' })).toBeVisible();

    await page.getByLabel('Rubric id').fill('lab.results');
    await page.getByLabel('Title').fill('Lab results');
    await page.getByLabel('Owner').fill('lcg');
    await page.getByLabel('Completeness').check();

    await page.getByRole('tab', { name: /Checks/ }).click();
    await page.getByLabel('Add a check of type').selectOption('COMPLETENESS');
    await page.getByRole('button', { name: 'Add check' }).click();
    await page.getByLabel('Check id').fill('result-present');
    await expect(page.getByLabel('Dimension')).toBeDisabled(); // fixed by the type
    await page.getByLabel('FHIRPath expression').fill('Observation.value.exists()');
    await page.getByRole('button', { name: 'Add check' }).click();
    await expect(page.getByRole('table', { name: 'Checks of this draft' })).toContainText('result-present');

    await page.getByRole('button', { name: 'Validate' }).click();
    await expect(page.getByRole('status')).toContainText('No problems found');
    expect(validated).toHaveLength(1);

    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page).toHaveURL(/\/rubrics\/lab\.results\/versions\/1\.0\.0\/edit$/);
    await expect(page.getByRole('status')).toContainText('Draft saved.');
    expect(saved).toHaveLength(1);
    expect(saved[0].headers['if-none-match']).toBe('*'); // a new rubric must not land on an existing one
    expect(saved[0].headers['if-match']).toBeUndefined();
    expect(saved[0].body).toEqual({
      id: 'lab.results',
      semver: '1.0.0',
      title: 'Lab results',
      owner: 'lcg',
      dimensions: ['CONFORMANCE', 'COMPLETENESS'],
      scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
      checks: [{
        id: 'result-present', type: 'COMPLETENESS', dimension: 'COMPLETENESS', severityOverride: 'ERROR',
        ordinal: 1, enabled: true, parameters: { expression: 'Observation.value.exists()' },
      }],
    });
  });

  test('a blank rubric is not saved: the problems are listed and nothing is sent', async ({ page, api }) => {
    await page.goto('/rubrics/-/new');
    await page.getByLabel('Title').fill('Something'); // so there is something to save

    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page.getByRole('alert')).toContainText('There are 2 problems to fix');
    await expect(page.getByText('Enter an id for the rubric.').first()).toBeVisible();
    expect(api.callsTo('POST', RUBRICS)).toHaveLength(0);
  });

  test('a check with problems is not added until they are fixed, or it is added anyway', async ({ page }) => {
    await page.goto('/rubrics/-/new');
    await page.getByRole('tab', { name: /Checks/ }).click();
    await page.getByRole('button', { name: 'Add check' }).click();

    await page.getByRole('button', { name: 'Add check' }).click();
    await expect(page.getByText('Enter an id for the check.')).toBeVisible();
    await expect(page.getByText('Enter a FHIRPath expression.')).toBeVisible();

    await page.getByRole('button', { name: 'Apply anyway' }).click();
    await expect(page.getByRole('table', { name: 'Checks of this draft' })).toContainText('(no id)');
  });
});

test.describe('a new version', () => {
  test('from a published version: opens a copy under the next free version number', async ({ page, api }) => {
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.1'));
    api.mock(`GET ${RUBRIC}/versions/1.2.1`, rubricOk({ ...draftVersion, semver: '1.2.1', definition: { ...publishedDefinition, semver: '1.2.1' } }));

    await page.goto('/rubrics/bare-minimum-requirements/versions/1.1.0');
    await page.getByRole('link', { name: 'New version from this' }).click();

    await expect(page.getByRole('heading', { name: 'New version of bare-minimum-requirements' })).toBeVisible();
    await expect(page.getByLabel('Rubric id')).toBeDisabled();
    // above the 1.2.0 draft that already exists, not just above the 1.1.0 being copied
    await expect(page.getByLabel('Version')).toHaveValue('1.2.1');
    await expect(page.getByText('Unsaved changes')).toBeVisible();

    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page).toHaveURL(/\/versions\/1\.2\.1\/edit$/);
    expect(saved[0].headers['if-none-match']).toBe('*');
    expect(saved[0].body.semver).toBe('1.2.1');
    expect(saved[0].body.checks).toHaveLength(publishedDefinition.checks.length);
  });

  test('the version can be stepped by patch, minor or major', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements/versions/new');
    await expect(page.getByLabel('Version')).toHaveValue('1.2.1');

    await page.getByRole('button', { name: 'minor', exact: true }).click();
    await expect(page.getByLabel('Version')).toHaveValue('1.3.0');
    await page.getByRole('button', { name: 'major', exact: true }).click();
    await expect(page.getByLabel('Version')).toHaveValue('2.0.0');
  });

  test('a version number that is taken is caught before saving', async ({ page, api }) => {
    await page.goto('/rubrics/bare-minimum-requirements/versions/new');
    await page.getByLabel('Version').fill('1.1.0');

    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page.getByText('Version 1.1.0 of this rubric already exists. Choose another version number.').first()).toBeVisible();
    expect(api.callsTo('POST', RUBRIC)).toHaveLength(0);
  });
});

test.describe('editing a draft', () => {
  const EDIT_URL = '/rubrics/bare-minimum-requirements/versions/1.2.0/edit';

  test('is reached from the rubric page and from the draft\'s own page', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements');
    await page.getByRole('link', { name: 'Edit draft 1.2.0' }).click();
    await expect(page).toHaveURL(new RegExp(`${EDIT_URL.replace(/\./g, '\\.')}$`));

    await page.goto('/rubrics/bare-minimum-requirements/versions/1.2.0');
    await page.getByRole('link', { name: 'Edit', exact: true }).click();
    await expect(page.getByText('All changes saved')).toBeVisible();
  });

  test('saves against the draft it opened, and Save is only offered when something changed', async ({ page, api }) => {
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));

    await page.goto(EDIT_URL);
    await expect(page.getByRole('button', { name: 'Save draft' })).toBeDisabled();

    await page.getByLabel('Owner').fill('lantana');
    await expect(page.getByText('Unsaved changes')).toBeVisible();
    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page.getByRole('status')).toContainText('Draft saved.');
    await expect(page.getByText('All changes saved')).toBeVisible();
    expect(saved[0].headers['if-match']).toBe(`"${draftVersion.checksum}"`);
    expect(saved[0].headers['if-none-match']).toBeUndefined();
    expect(saved[0].body.owner).toBe('lantana');
  });

  test('checks can be edited, reordered, disabled, duplicated and deleted', async ({ page, api }) => {
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));

    await page.goto(EDIT_URL);
    await page.getByRole('tab', { name: /Checks/ }).click();

    await page.getByRole('button', { name: 'Edit patient-name-present' }).click();
    await page.getByLabel('Severity').selectOption('ERROR');
    await page.getByRole('button', { name: 'Apply', exact: true }).click();

    await page.getByRole('button', { name: 'Move patient-name-present up' }).click();
    await page.getByRole('checkbox', { name: 'Enable diagnosticreport-category-required' }).uncheck();
    await page.getByRole('button', { name: 'Duplicate diagnosticreport-category-terminology' }).click();
    await page.getByRole('button', { name: 'Delete specimen-type-terminology' }).click();
    await expect(page.getByRole('tab', { name: 'Checks (5)' })).toBeVisible();

    await page.getByRole('button', { name: 'Save draft' }).click();
    await expect(page.getByRole('status')).toContainText('Draft saved.');

    expect(saved[0].body.checks.map((c: any) => [c.id, c.ordinal, c.enabled, c.severityOverride])).toEqual([
      ['diagnosticreport-category-required', 1, false, 'ERROR'],
      ['diagnosticreport-category-terminology', 2, true, 'ERROR'],
      ['diagnosticreport-category-terminology-copy', 3, true, 'ERROR'],
      ['patient-name-present', 4, true, 'ERROR'],
      ['patient-conformance', 5, false, 'INFORMATION'],
    ]);
  });

  test('a combined check is edited as a list of the checks it combines', async ({ page, api }) => {
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));

    await page.goto(EDIT_URL);
    await page.getByRole('tab', { name: /Checks/ }).click();
    await page.getByRole('button', { name: 'Edit specimen-type-terminology' }).click();

    await expect(page.getByRole('radio', { name: 'Several checks combined' })).toBeChecked();
    await page.getByLabel('Passes when').selectOption('ALL');
    await page.getByRole('button', { name: 'Add a check to combine' }).click();
    const third = page.getByRole('group', { name: 'Check 3' });
    await third.getByLabel('Id').fill('has-text');
    await third.getByLabel('FHIRPath expression').fill('DiagnosticReport.specimen.type.text.exists()');
    await page.getByRole('button', { name: 'Remove check 1' }).click();
    await page.getByRole('button', { name: 'Apply', exact: true }).click();

    await page.getByRole('button', { name: 'Save draft' }).click();
    await expect(page.getByRole('status')).toContainText('Draft saved.');

    const combined = saved[0].body.checks.find((c: any) => c.id === 'specimen-type-terminology');
    expect(combined.parameters.matchMode).toBe('ALL');
    expect(combined.parameters.checks).toEqual([
      { id: 'valueset', type: 'VALUESET', parameters: { path: 'DiagnosticReport.specimen.type', valueSet: 'http://hl7.org/fhir/ValueSet/resource-types|4.0.1' } },
      { id: 'has-text', type: 'FHIRPATH', parameters: { expression: 'DiagnosticReport.specimen.type.text.exists()' } },
    ]);
  });

  test('what the service finds wrong is shown on the check it is about, and nothing is saved', async ({ page, api }) => {
    api.mock(`POST ${RUBRICS}/$validate`, validationResult({
      valid: false, errors: ['checks[patient-name-present]: invalid FHIRPath expression: unexpected token'],
    }));

    await page.goto(EDIT_URL);
    await page.getByRole('button', { name: 'Validate' }).click();

    await expect(page.getByRole('alert')).toContainText('There is a problem to fix');
    await expect(page.getByRole('tab', { name: /Checks \(5\) \(1 to fix\)/ })).toBeVisible();
    await page.getByRole('button', { name: 'Check patient-name-present' }).click();
    await expect(page.getByRole('heading', { name: 'Edit check patient-name-present' })).toBeVisible();
    await expect(page.getByText('invalid FHIRPath expression: unexpected token').last()).toBeVisible();
    expect(api.callsTo('POST', `${RUBRIC}/versions`)).toHaveLength(0);
  });

  test('a draft someone else changed is not overwritten; it can be reloaded', async ({ page, api }) => {
    api.mock(`POST ${RUBRIC}/versions`, rubricError(412, 'Rubric bare-minimum-requirements v1.2.0 has been changed since it was read'));

    await page.goto(EDIT_URL);
    await page.getByLabel('Owner').fill('mine');
    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page.getByRole('alert')).toContainText('changed by someone else after you opened it');
    await expect(page.getByText('Unsaved changes')).toBeVisible();

    api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk({ ...draftVersion, checksum: 'f'.repeat(64), definition: { ...draftDefinition, owner: 'theirs' } }));
    await page.getByRole('button', { name: 'Reload their version' }).click();

    await expect(page.getByLabel('Owner')).toHaveValue('theirs');
    await expect(page.getByText('All changes saved')).toBeVisible();
  });

  test('or saved anyway, which sends no precondition', async ({ page, api }) => {
    api.mock(`POST ${RUBRIC}/versions`, rubricError(412, 'changed'));
    await page.goto(EDIT_URL);
    await page.getByLabel('Owner').fill('mine');
    await page.getByRole('button', { name: 'Save draft' }).click();
    await expect(page.getByRole('alert')).toContainText('changed by someone else');

    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));
    await page.getByRole('button', { name: 'Save mine anyway' }).click();

    await expect(page.getByRole('status')).toContainText('Draft saved.');
    expect(saved[0].headers['if-match']).toBeUndefined();
    expect(saved[0].headers['if-none-match']).toBeUndefined();
    expect(saved[0].body.owner).toBe('mine');
  });

  test('changing the version moves the draft: saved under the new number, the old one deleted', async ({ page, api }) => {
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '2.0.0'));
    api.mock(`DELETE ${RUBRIC}/versions/1.2.0`, { status: 204 });
    api.mock(`GET ${RUBRIC}/versions/2.0.0`, rubricOk({ ...draftVersion, semver: '2.0.0', definition: { ...draftDefinition, semver: '2.0.0' } }));

    await page.goto(EDIT_URL);
    await page.getByRole('button', { name: 'major', exact: true }).click();
    await expect(page.getByLabel('Version')).toHaveValue('2.0.0');
    await page.getByRole('button', { name: 'Save draft' }).click();

    await expect(page).toHaveURL(/\/versions\/2\.0\.0\/edit$/);
    await expect(page.getByRole('status')).toContainText('Draft saved as version 2.0.0; the draft under 1.2.0 was deleted.');
    expect(saved[0].headers['if-none-match']).toBe('*');
    expect(api.callsTo('DELETE', `${RUBRIC}/versions/1.2.0`)).toHaveLength(1);
  });

  test('a published version cannot be edited; the page says how to change the rubric', async ({ page }) => {
    await page.goto('/rubrics/bare-minimum-requirements/versions/1.1.0/edit');

    await expect(page.getByRole('alert')).toContainText('This version cannot be edited.');
    await expect(page.getByRole('alert')).toContainText('Version 1.1.0 is published and cannot be changed');
    await expect(page.getByRole('button', { name: 'Save draft' })).toHaveCount(0);
    await page.getByRole('link', { name: 'Create a new version from it' }).click();
    await expect(page).toHaveURL(/\/versions\/new\?from=1\.1\.0$/);
  });

  test('a custom check is shown read-only and saved exactly as it came', async ({ page, api }) => {
    const custom = { id: 'plug-in', type: 'CUSTOM' as const, dimension: 'CONFORMANCE' as const, severityOverride: 'ERROR' as const, ordinal: 6, enabled: true, parameters: { customCheckId: 'my-plugin', min: 1 } };
    api.mock(`GET ${RUBRIC}/versions/1.2.0`, rubricOk({ ...draftVersion, definition: { ...draftDefinition, checks: [...draftDefinition.checks, custom] } }));
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));

    await page.goto(EDIT_URL);
    await page.getByRole('tab', { name: /Checks/ }).click();
    await expect(page.getByRole('row', { name: /plug-in/ })).toContainText('Read-only');
    await page.getByRole('button', { name: 'View plug-in' }).click();
    await expect(page.getByText('Custom checks cannot be edited here.')).toBeVisible();
    await expect(page.getByLabel('Check id')).toHaveCount(0);
    await page.getByRole('button', { name: 'Close' }).click();

    await page.getByRole('tab', { name: 'Details' }).click();
    await page.getByLabel('Owner').fill('lantana');
    await page.getByRole('button', { name: 'Save draft' }).click();
    await expect(page.getByRole('status')).toContainText('Draft saved.');

    expect(saved[0].body.checks.at(-1)).toEqual({ ...custom, ordinal: 6 });
  });
});

test.describe('leaving the editor', () => {
  test('asks first when there are unsaved changes, and stays when the answer is no', async ({ page }) => {
    const prompts = answerLeavePrompts(page, false);

    await page.goto('/rubrics/bare-minimum-requirements/versions/1.2.0/edit');
    await page.getByLabel('Owner').fill('changed');
    await page.getByRole('link', { name: 'Validation Rubrics' }).click();

    await expect(page).toHaveURL(/\/versions\/1\.2\.0\/edit$/);
    await expect(page.getByLabel('Owner')).toHaveValue('changed');
    expect(prompts.asked).toBe(1);
  });

  test('leaves when the answer is yes', async ({ page }) => {
    answerLeavePrompts(page, true);

    await page.goto('/rubrics/bare-minimum-requirements/versions/1.2.0/edit');
    await page.getByLabel('Owner').fill('changed');
    await page.getByRole('link', { name: 'Validation Rubrics' }).click();

    await expect(page).toHaveURL(/\/rubrics$/);
  });

  test('does not ask when nothing was changed', async ({ page }) => {
    const prompts = answerLeavePrompts(page, false);

    await page.goto('/rubrics/bare-minimum-requirements/versions/1.2.0/edit');
    await expect(page.getByText('All changes saved')).toBeVisible();
    await page.getByRole('link', { name: 'Validation Rubrics' }).click();

    await expect(page).toHaveURL(/\/rubrics$/);
    expect(prompts.asked).toBe(0);
  });
});

test.describe('importing a rubric', () => {
  const YAML = 'id: lab.results\nsemver: 1.0.0\ndimensions:\n  - CONFORMANCE\n';
  const imported = { ...publishedDefinition, id: 'lab.results', semver: '1.0.0', title: 'Lab results' };

  test('pasted YAML is reviewed, then imported as a draft and opened', async ({ page, api }) => {
    const reviewed = capture(api, `POST ${RUBRICS}/$validate`, validationResult({ definition: imported, checksum: 'new', rubricExists: false }));
    const saved = capture(api, `POST ${RUBRICS}/lab.results/versions`, savedDraft('lab.results', '1.0.0'));
    api.mock(`GET ${RUBRICS}/lab.results/versions/1.0.0`, rubricOk({ ...draftVersion, rubricId: 'lab.results', semver: '1.0.0', definition: imported }));

    await page.goto('/rubrics');
    await page.getByRole('link', { name: 'Import' }).click();
    await page.getByLabel('Definition, as YAML or JSON').fill(YAML);
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByText('Importing creates the rubric')).toBeVisible();
    await expect(page.getByText('4 (3 enabled)')).toBeVisible();
    expect(reviewed[0].headers['content-type']).toContain('application/yaml');
    expect(reviewed[0].body).toBe(YAML.trim());
    expect(api.callsTo('POST', `${RUBRICS}/lab.results`)).toHaveLength(0); // reviewing saves nothing

    await page.getByRole('button', { name: 'Import as a draft' }).click();

    await expect(page).toHaveURL(/\/rubrics\/lab\.results\/versions\/1\.0\.0$/);
    expect(saved[0].headers['if-none-match']).toBe('*');
    expect(saved[0].body).toEqual(imported); // what was reviewed is what is saved
  });

  test('an uploaded file fills the text and is sent as its type', async ({ page, api }) => {
    const reviewed = capture(api, `POST ${RUBRICS}/$validate`, validationResult({ definition: imported, checksum: 'new' }));

    await page.goto('/rubrics/-/import');
    await page.getByLabel('Choose a file').setInputFiles({
      name: 'lab-results.json', mimeType: 'application/json', buffer: Buffer.from(JSON.stringify(imported)),
    });
    await expect(page.getByText('lab-results.json')).toBeVisible();
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByText('Importing creates the rubric')).toBeVisible();
    expect(reviewed[0].headers['content-type']).toContain('application/json');
    expect(reviewed[0].body).toEqual(imported);
  });

  test('a file that is not YAML or JSON is refused', async ({ page }) => {
    await page.goto('/rubrics/-/import');
    await page.getByLabel('Choose a file').setInputFiles({ name: 'rubric.docx', mimeType: 'application/octet-stream', buffer: Buffer.from('x') });

    await expect(page.getByText('"rubric.docx" is not a .yaml, .yml or .json file.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Review' })).toBeDisabled();
  });

  test('a definition with problems lists them, cannot be imported, and can be opened in the editor', async ({ page, api }) => {
    api.mock(`POST ${RUBRICS}/$validate`, validationResult({
      valid: false, definition: imported, rubricExists: false,
      errors: ['checks[diagnosticreport-category-required]: invalid FHIRPath expression: unexpected token'],
    }));

    await page.goto('/rubrics/-/import');
    await page.getByLabel('Definition, as YAML or JSON').fill(YAML);
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByRole('alert')).toContainText('This definition has a problem and cannot be imported as it is.');
    await expect(page.getByRole('alert')).toContainText('invalid FHIRPath expression: unexpected token');
    await expect(page.getByRole('button', { name: 'Import as a draft' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Open in the editor' }).click();

    await expect(page).toHaveURL(/\/rubrics\/-\/new$/);
    await expect(page.getByLabel('Rubric id')).toHaveValue('lab.results');
    await expect(page.getByRole('tab', { name: 'Checks (4)' })).toBeVisible();
    await expect(page.getByText('Unsaved changes')).toBeVisible();
  });

  test('text that is not a definition says why it could not be read', async ({ page, api }) => {
    api.mock(`POST ${RUBRICS}/$validate`, validationResult({ valid: false, errors: ['Malformed YAML rubric payload: mapping values are not allowed here'] }));

    await page.goto('/rubrics/-/import');
    await page.getByLabel('Definition, as YAML or JSON').fill('this: is: not: yaml');
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByRole('alert')).toContainText('This could not be read as a rubric definition.');
    await expect(page.getByRole('alert')).toContainText('mapping values are not allowed here');
  });

  test('replacing an existing draft says so, and only replaces the draft that was reviewed', async ({ page, api }) => {
    api.mock(`POST ${RUBRICS}/$validate`, validationResult({
      definition: draftDefinition, checksum: 'new', rubricExists: true, existingStatus: 'DRAFT', existingChecksum: draftVersion.checksum,
    }));
    const saved = capture(api, `POST ${RUBRIC}/versions`, savedDraft('bare-minimum-requirements', '1.2.0'));

    await page.goto('/rubrics/-/import');
    await page.getByLabel('Definition, as YAML or JSON').fill(YAML);
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByText('replaces that draft')).toBeVisible();
    await page.getByRole('button', { name: 'Import as a draft' }).click();

    await expect(page).toHaveURL(/\/versions\/1\.2\.0$/);
    expect(saved[0].headers['if-match']).toBe(`"${draftVersion.checksum}"`);
  });

  test('a published version cannot be replaced; the definition can be reviewed as the next version instead', async ({ page, api }) => {
    const validations: Sent[] = [];
    api.mock(`POST ${RUBRICS}/$validate`, async (route) => {
      const text = route.request().postData() ?? '';
      const asJson = text.trim().startsWith('{');
      validations.push({ url: route.request().url(), headers: route.request().headers(), body: asJson ? JSON.parse(text) : text });
      const answer = asJson
        ? validationResult({ definition: JSON.parse(text), checksum: 'new', rubricExists: true })
        : validationResult({ definition: publishedDefinition, checksum: 'different', rubricExists: true, existingStatus: 'PUBLISHED', existingChecksum: publishedVersion.checksum });
      await route.fulfill({ status: answer.status, json: answer.json });
    });

    await page.goto('/rubrics/-/import');
    await page.getByLabel('Definition, as YAML or JSON').fill(YAML);
    await page.getByRole('button', { name: 'Review' }).click();

    await expect(page.getByRole('alert')).toContainText('Version 1.1.0 of bare-minimum-requirements is published and cannot be changed.');
    await expect(page.getByRole('button', { name: 'Import as a draft' })).toHaveCount(0);

    await page.getByRole('button', { name: 'Review it as the next version' }).click();

    await expect(page.getByText('Importing adds version 1.2.1')).toBeVisible();
    expect(validations[1].body.semver).toBe('1.2.1'); // above the 1.2.0 draft the rubric already has
    await expect(page.getByRole('button', { name: 'Import as a draft' })).toBeEnabled();
  });
});
