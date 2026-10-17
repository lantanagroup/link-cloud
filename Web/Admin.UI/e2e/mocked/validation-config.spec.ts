import { test, expect } from '../support/test';

const artifacts = [
  { id: 1, name: 'us.nhsn.dqm.package', type: 'PACKAGE' },
  { id: 2, name: 'uscore.package', type: 'PACKAGE' },
];

const categories = [
  { id: 'cat-1', title: 'Reference Not Resolvable', severity: 'Warning', acceptable: true, guidance: 'Safe to accept.' },
  { id: 'cat-2', title: 'Invalid Code', severity: 'Error', acceptable: false, guidance: 'Must be fixed upstream.' },
];

test('validation config page renders artifacts', async ({ page, api }) => {
  api.mock('GET /api/validation/artifact', artifacts);
  api.mock('GET /api/validation/artifact/tx-dependencies', []);

  await page.goto('/validation-config');

  await expect(page.getByText('us.nhsn.dqm.package')).toBeVisible();
  await expect(page.getByText('uscore.package')).toBeVisible();
});

test('validation categories list renders mocked categories', async ({ page, api }) => {
  api.mock('GET /api/validation/category', categories);

  await page.goto('/validation-config/validation-categories');

  await expect(page.getByText('Reference Not Resolvable')).toBeVisible();
  await expect(page.getByText('Invalid Code')).toBeVisible();
});
