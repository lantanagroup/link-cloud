import { test, expect } from '../support/test';
import { NavPage } from '../support/pages/nav.page';

test('dashboard renders the shell without any API calls', async ({ page, api }) => {
  await page.goto('/dashboard');
  const nav = new NavPage(page);
  await nav.expectShellVisible();

  // AdminDashboardComponent reads only session storage; hitting /api here would
  // mean a regression (or a fixture gap elsewhere).
  expect(api.calls).toHaveLength(0);
  expect(api.unmatched).toHaveLength(0);
});

test('top-level nav links route to their screens', async ({ page, api }) => {
  // /tenant loads the paged facility list AND the autocomplete lookup on arrival.
  api.mock('GET /api/facility', { records: [], metadata: { totalCount: 0, pageNumber: 1, pageSize: 10 } });
  api.mock('GET /api/facility/list', {});

  await page.goto('/dashboard');
  const nav = new NavPage(page);
  await nav.clickNav('Tenants');
  await page.waitForURL('**/tenant');
  await nav.clickNav('Home');
  await page.waitForURL('**/dashboard');
});
