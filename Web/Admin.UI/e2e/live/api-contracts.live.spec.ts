import { test, expect, APIRequestContext } from '@playwright/test';
import { apiBaseUrl, assertBackendReachable } from '../support/live';

/**
 * Shape checks for the endpoints the mocked tier fakes.
 *
 * The mocked specs assert against fixtures, so they stay green forever even if the BFF
 * renames a field or an endpoint — nothing in that tier can detect backend drift. These
 * tests close that gap: they call the real BFF and assert that the fields the UI reads
 * still exist, with the right types.
 *
 * Deliberately shape-only. No seeded-data assumptions: anything needing a facility in a
 * particular state creates it and deletes it again, so the suite is repeatable and leaks
 * nothing into the dotnet categories that run after it in CI.
 */

test.beforeAll(async ({ request }) => {
  await assertBackendReachable(request);
});

/** The paging envelope every list endpoint in the UI is built around. */
function expectPagedEnvelope(body: unknown): void {
  const paged = body as { records?: unknown; metadata?: Record<string, unknown> };
  expect(Array.isArray(paged.records), 'records should be an array').toBe(true);
  expect(paged.metadata, 'paging metadata should be present').toBeTruthy();
  expect(typeof paged.metadata?.['totalCount']).toBe('number');
}

/** Creates a facility for the duration of `body`, then removes it whatever happens. */
async function withFacility(
  request: APIRequestContext,
  body: (facilityId: string) => Promise<void>,
): Promise<void> {
  const api = apiBaseUrl();
  const facilityId = `ui-contract-${Date.now()}`;

  const create = await request.post(`${api}/facility`, {
    data: {
      facilityId,
      facilityName: 'UI Contract Check Facility',
      timeZone: 'America/New_York',
      vendor: 'Epic',
      scheduledReports: { daily: [], weekly: [], monthly: [] },
    },
  });
  expect(create.ok(), `POST /facility failed: ${create.status()} ${await create.text()}`).toBe(true);

  // Creation is not immediately readable on a busy stack.
  await expect
    .poll(async () => (await request.get(`${api}/facility/${facilityId}`)).status(), {
      message: `facility ${facilityId} never became readable`,
      timeout: 30_000,
    })
    .toBe(200);

  try {
    await body(facilityId);
  } finally {
    const del = await request.delete(`${api}/facility/${facilityId}`);
    expect([200, 202, 204, 404]).toContain(del.status());
  }
}

test('GET /facility returns the paged envelope and the fields the tenant table renders', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const response = await request.get(
      `${api}/facility?facilityId=${facilityId}&facilityName=&sortBy=FacilityId&sortOrder=0&pageSize=10&pageNumber=1&includeDeleted=false`,
    );
    expect(response.status()).toBe(200);

    const body = await response.json();
    expectPagedEnvelope(body);

    // Columns rendered by tenant-dashboard.component.html.
    const record = body.records[0];
    expect(typeof record.facilityId).toBe('string');
    expect(typeof record.facilityName).toBe('string');
    expect(typeof record.timeZone).toBe('string');
    expect(typeof record.vendor).toBe('string');
    expect(typeof record.isDeleted).toBe('boolean');
  });
});

test('GET /facility/:id returns the fields the edit form binds', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const response = await request.get(`${api}/facility/${facilityId}`);
    expect(response.status()).toBe(200);

    const facility = await response.json();
    expect(facility.facilityId).toBe(facilityId);
    expect(typeof facility.facilityName).toBe('string');
    expect(typeof facility.timeZone).toBe('string');
    expect(facility.scheduledReports).toBeTruthy();
    for (const cadence of ['daily', 'weekly', 'monthly']) {
      expect(Array.isArray(facility.scheduledReports[cadence]), `scheduledReports.${cadence}`).toBe(true);
    }
  });
});

/**
 * `search` is a free-text fragment matched against both the facility name and the facility
 * id (Tenant's FacilityQueries.PagedSearchAsync, under PartialMatch). The tenant dashboard's
 * "Facility" filter feeds this endpoint with whatever the user typed, so both halves matter:
 * part of a name and part of an id must each find the facility.
 *
 * This previously asserted that an id search found nothing — a characterisation of the bug
 * where the dropdown offered a facility the table underneath then reported as missing.
 */
test('GET /facility/list matches on both name and id', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const byName = await request.get(`${api}/facility/list?search=UI Contract Check&includeDeleted=false`);
    expect(byName.status(), 'name search should match').toBe(200);

    // A map of facilityId -> facilityName, which is what the autocomplete is built from.
    const nameLookup = await byName.json();
    expect(typeof nameLookup).toBe('object');
    expect(nameLookup[facilityId]).toBe('UI Contract Check Facility');

    const byId = await request.get(`${api}/facility/list?search=${facilityId}&includeDeleted=false`);
    expect(byId.status(), 'id search should match').toBe(200);

    const idLookup = await byId.json();
    expect(typeof idLookup).toBe('object');
    expect(idLookup[facilityId]).toBe('UI Contract Check Facility');
  });
});

/**
 * Kept from the test above, which used to cover this incidentally through its id search: an
 * empty result is 204 with no body rather than 200 with an empty map. Callers have to treat
 * "no matches" as a status code, not as an empty collection.
 */
test('GET /facility/list 204s when nothing matches', async ({ request }) => {
  const api = apiBaseUrl();
  const term = `no-such-facility-${Date.now()}`;

  const response = await request.get(`${api}/facility/list?search=${term}&includeDeleted=false`);
  expect(response.status(), 'an empty result is 204, no body').toBe(204);
});

/**
 * The contract e2e/mocked/query-dispatch-create.spec.ts encodes: the GET 404s until a POST
 * lands, the POST body carries no `event` (Angular omits disabled controls from form.value),
 * and the backend defaults it to Discharge on the way back out.
 */
test('query dispatch configuration round-trips with event defaulted by the backend', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const missing = await request.get(`${api}/querydispatch/configuration/facility/${facilityId}`);
    expect(missing.status(), 'an unconfigured facility should 404').toBe(404);

    const created = await request.post(`${api}/querydispatch/configuration`, {
      data: { facilityId, dispatchSchedules: [{ duration: 'PT10S' }] },
    });
    expect(created.status(), await created.text()).toBe(201);

    try {
      const response = await request.get(`${api}/querydispatch/configuration/facility/${facilityId}`);
      expect(response.status()).toBe(200);

      const config = await response.json();
      expect(config.facilityId).toBe(facilityId);
      expect(Array.isArray(config.dispatchSchedules)).toBe(true);
      expect(config.dispatchSchedules[0].duration).toBe('PT10S');
      expect(config.dispatchSchedules[0].event, 'backend defaults the omitted event').toBe('Discharge');
    } finally {
      const del = await request.delete(`${api}/querydispatch/configuration/facility/${facilityId}`);
      expect([200, 202, 204, 404]).toContain(del.status());
    }
  });
});

/** Same contract shape for census, which the facility edit page treats identically. */
test('census configuration round-trips with the fields the panel binds', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const missing = await request.get(`${api}/census/config/${facilityId}`);
    expect(missing.status(), 'an unconfigured facility should 404').toBe(404);

    const created = await request.post(`${api}/census/config`, {
      data: { facilityId, scheduledTrigger: '0 0 6 * * ?', enabled: true },
    });
    expect(created.status(), await created.text()).toBe(201);

    try {
      const response = await request.get(`${api}/census/config/${facilityId}`);
      expect(response.status()).toBe(200);

      const config = await response.json();
      expect(config.facilityId).toBe(facilityId);
      expect(typeof config.scheduledTrigger).toBe('string');
      expect(typeof config.enabled).toBe('boolean');
    } finally {
      const del = await request.delete(`${api}/census/config/${facilityId}`);
      expect([200, 202, 204, 404]).toContain(del.status());
    }
  });
});

test('GET /measureeval/measure-definition returns identifiable definitions', async ({ request }) => {
  const response = await request.get(`${apiBaseUrl()}/measureeval/measure-definition`);

  // A freshly built stack has no measure definitions (see facility-roundtrip.live.spec.ts),
  // and this BFF answers an empty collection with 204 and no body. Asserting 200 outright
  // would fail every CI run while passing locally, where the stack has been seeded.
  expect([200, 204]).toContain(response.status());
  if (response.status() !== 200) return;

  const definitions = await response.json();
  expect(Array.isArray(definitions)).toBe(true);
  // The create-facility and generate-report screens key their options off `id`.
  for (const definition of definitions) {
    expect(typeof definition.id, 'every measure definition needs an id').toBe('string');
  }
});

test('the per-facility list endpoints the facility screens page through keep their envelope', async ({ request }) => {
  const api = apiBaseUrl();

  await withFacility(request, async (facilityId) => {
    const endpoints = [
      `${api}/aggregate/reports/summaries?pageSize=10&pageNumber=1&facilityId=${facilityId}&includeDeleted=false&sortBy=CreateDate&sortOrder=1`,
      `${api}/data/location-mappings/facility/${facilityId}/search?pageNumber=1&pageSize=10&IsActive=true`,
      `${api}/data/encounter-mappings/facilities/${facilityId}/search?pageNumber=1&pageSize=10`,
      `${api}/normalization/operations/facility/${facilityId}?pageNumber=1&sortBy=OperationType&sortOrder=ascending`,
    ];

    for (const url of endpoints) {
      const response = await request.get(url);
      expect(response.status(), `GET ${url}`).toBe(200);
      expectPagedEnvelope(await response.json());
    }
  });
});
