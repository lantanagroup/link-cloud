import { test, expect } from '@playwright/test';
import { TenantDashboardPage } from '../support/pages/tenant-dashboard.page';
import { apiBaseUrl, assertBackendReachable } from '../support/live';

test.beforeAll(async ({ request }) => {
  await assertBackendReachable(request);
});

// Creates and removes its own facility via the BFF API (the create dialog needs measure
// definitions, which a fresh stack does not have), and uses the UI to verify both states.
// Runs before the backend dotnet test categories in CI, so cleanup in finally is mandatory
// to avoid leaking state into them.

test('a facility created through the BFF appears in the UI and disappears after deletion', async ({ page, request }) => {
  const facilityId = `ui-e2e-${Date.now()}`;
  // Held in a const because the dashboard filter searches on name — see below — so the
  // search term and the created facility must not be able to drift apart.
  const facilityName = 'UI E2E Roundtrip Facility';
  const api = apiBaseUrl();

  try {
    const create = await request.post(`${api}/facility`, {
      data: {
        facilityId,
        facilityName,
        timeZone: 'America/New_York',
        vendor: 'Epic',
        scheduledReports: { daily: [], weekly: [], monthly: [] },
      },
    });
    expect(create.ok(), `POST /facility failed: ${create.status()} ${await create.text()}`).toBe(true);

    // Creation is not immediately readable on a busy stack — wait until the backend
    // itself serves the facility before asserting anything about the UI.
    await expect
      .poll(async () => (await request.get(`${api}/facility/${facilityId}`)).status(), {
        message: `facility ${facilityId} never became readable after creation`,
        timeout: 30_000,
      })
      .toBe(200);

    const tenants = new TenantDashboardPage(page);
    await tenants.goto();

    // Search by name, not id: the filter sends the term as facilityName, and the Tenant
    // service matches it with FacilityName.Contains (FacilityController.GetFacilityList ->
    // FacilityQueries.PagedSearchAsync). Typing the id filters everything out, which an
    // earlier version of this test hid by also accepting an autocomplete option — those
    // options are rendered from the *unfiltered* lookup and carry the id, so the assertion
    // passed without the search ever working.
    await tenants.facilitySearch.fill(facilityName);

    // The row is the real outcome: it fails if either the search or the table breaks.
    await expect(tenants.rowFor(facilityId)).toBeVisible({ timeout: 15_000 });
  } finally {
    const del = await request.delete(`${api}/facility/${facilityId}`);
    // 404 is acceptable if creation itself failed.
    expect([200, 202, 204, 404]).toContain(del.status());
  }

  // Verify it is gone from the real list.
  const check = await request.get(`${api}/facility/${facilityId}`);
  expect(check.status(), 'facility should no longer exist').not.toBe(200);
});
