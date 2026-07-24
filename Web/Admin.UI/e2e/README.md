# Admin.UI end-to-end tests (Playwright)

Two tiers, selected via Playwright projects:

| Project  | What it tests                     | Backend                | Run with |
|----------|-----------------------------------|------------------------|----------|
| `mocked` | UI behavior against API fixtures  | None (all `/api` intercepted) | `npm run e2e` |
| `live`   | Real integration through the BFF  | Running Link stack     | `npm run e2e:live` |

## Mocked tier (default)

```
npm run e2e        # headless
npm run e2e:ui     # interactive UI mode
npm run e2e:report # open the last HTML report
```

Nothing else needs to be running — Playwright boots `ng serve` itself (`webServer` in
`playwright.config.ts`). Every test gets:

- a deterministic app config (`support/app-config.ts` intercepts
  `/assets/app.config.local.json`, so your personal local config never affects tests);
  auth is disabled unless a spec opts in via `test.use({ appConfig: { authRequired: true } })`
- an `/api` catch-all (`support/api-mock.ts`): unmocked endpoints return 404 and are
  recorded in `api.unmatched` — assert it stays empty to catch missing fixtures

Fixtures live in `e2e/fixtures/` as **typed TypeScript** against the app's own interfaces
(`src/app/interfaces/...`), so backend model drift breaks the build instead of silently
passing. See `tools/capture-fixtures.md` for how to capture new ones.

## Live tier

Small smoke set (`e2e/live/*.live.spec.ts`) against a real stack. No mocks, no seeded-data
assumptions — assertions are shape-based (2xx responses, tables or empty states render).

**Default target is the docker compose stack** — UI at `http://localhost:8066`, BFF
cross-origin at `http://localhost:8063/api` (override with `UI_E2E_API_URL`):

```powershell
npm run e2e:live
```

A preflight pings the BFF first and fails immediately with instructions if the stack
isn't reachable. To run live against `ng serve` instead (requires a local BFF on 5218,
see `proxy.conf.json`):

```powershell
$env:UI_E2E_BASE_URL='http://localhost:4200'; npm run e2e:live
```

In CI the live project runs inside the `backend-e2e-tests` job (`.github/workflows/tests.yaml`)
against the already-running compose stack, after the health check and before the backend
test categories. Its report uploads as the `admin-ui-e2e-report` artifact.

Note: helmet/CSP in `server/main.js` is enabled only when `production: true`. If
`LINK_PRODUCTION=true` is ever set on the compose admin-ui service, its default CSP will
block the cross-origin BFF calls and these tests will fail — that is a real config problem,
not a test bug.

## Conventions

- Page objects in `e2e/support/pages/` are shared by both tiers — that is the selector
  contract. Prefer `page.getByTestId(...)` (add `data-testid` attributes to templates) and
  role/label selectors for Material controls.
- Mocked specs import `test` from `../support/test`; live specs import from
  `@playwright/test` directly (no mock fixtures).
