# Capturing API fixtures for the mocked tier

Fixtures are small, hand-minimized, **typed** TypeScript modules in `e2e/fixtures/`.
Do not commit raw HAR files or full recorded payloads — huge diffs, opaque failures.

## Procedure

1. Start the stack: `docker compose up -d` (or at least the BFF + the service you need).
2. Capture the real response for the endpoint you need, e.g.:

   ```powershell
   curl http://localhost:8063/api/facility | ConvertFrom-Json | ConvertTo-Json -Depth 10
   ```

   or click through the UI at http://localhost:8066 with browser devtools open and copy
   the response from the Network tab.
3. Trim it to the fields the UI actually reads (check the component/service that consumes
   it), then add it to an `e2e/fixtures/*.ts` module **typed against the app's interface**:

   ```ts
   import { PagedFacilityConfigModel } from '../../src/app/interfaces/tenant/facility-config-model.interface';
   ```

   If the interface doesn't cover the endpoint yet, that is a gap worth fixing in
   `src/app/interfaces/` first — the fixture then inherits the fix.
4. Register it in the spec: `api.mock('GET /api/whatever', fixture)`.

## Finding out which fixtures a page needs

Write the spec with only the fixtures you know about and assert
`expect(api.unmatched).toHaveLength(0)` — the failure message lists every unmocked
endpoint the page called, with method and path.
