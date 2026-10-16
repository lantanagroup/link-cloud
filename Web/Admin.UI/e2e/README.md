# Admin.UI end-to-end tests (Playwright)

Presenting this suite to a team? See [DEMO.md](DEMO.md) for a paced 10-minute runbook.

Two tiers, selected via Playwright projects:

| Project  | What it tests                     | Backend                | Run with |
|----------|-----------------------------------|------------------------|----------|
| `mocked` | UI behavior against API fixtures  | None (all `/api` intercepted) | `npm run e2e` |
| `live`   | Real integration through the BFF  | Running Link stack     | `npm run e2e:live` |

## Mocked tier (default)

```powershell
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
(`src/app/interfaces/...`), so model drift surfaces as a type error on the fixture rather than
a stale shape that silently passes. Playwright transpiles with esbuild and does not typecheck,
so that guarantee needs `tsc` to run:

```powershell
npm run typecheck:e2e   # tsc --noEmit -p e2e/tsconfig.json
```

CI runs it in the `Admin UI Tests (mocked)` job before installing browsers. See
`tools/capture-fixtures.md` for how to capture new fixtures.

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

To leave this tier out of a run, set `RUN_LIVE_UI: "false"` in the `backend-e2e-tests`
env block in `.github/workflows/tests.yaml` and push. The live tests and the Playwright
install they need are skipped; the stack, the backend categories and the mocked UI job all
run as normal. It is committed, so it shows up in the diff — do not merge with it off.

Note: helmet/CSP in `server/main.js` is enabled only when `production: true`. If
`LINK_PRODUCTION=true` is ever set on the compose admin-ui service, its default CSP will
block the cross-origin BFF calls and these tests will fail — that is a real config problem,
not a test bug.

## What this costs in CI

Both tiers together add about **10 seconds** to a pull request. Worth knowing, because the
question comes up and the intuition is usually wrong.

The mocked tier is its own job, so it runs beside the backend work and finishes while that
job is still building images — it contributes nothing to the pipeline's wall clock, however
long it takes. The live tier runs as steps inside `backend-e2e-tests`, which does set the
wall clock, but its toolchain installs ahead of the stack health check: that wait shrinks by
however long the install took, so most of the cost overlaps time the job was already
spending. What is left is the tests themselves.

Measured on run #3714 (2026-10-16): install 27s + health wait 11s + tests 10s + upload 1s =
49s, against ~40s for the health wait alone without the tier. The mocked job was 2m 54s, in
parallel. For comparison, that job spends 4m building images and 2m starting the stack
before any test runs, and its backend categories alone vary by a minute between runs on
identical code — so this difference is far smaller than the noise and is only visible in the
per-step durations, never in the run total.

To re-measure: open the run, click the `Backend E2E Tests with Docker Compose` job, and read
the step durations. Do not compare run totals.

One asymmetry to be aware of: `Tests / Backend E2E Tests with Docker Compose` is a required
check on `dev` and `Tests / Admin UI Tests (mocked)` is not, so a live-tier failure blocks a
merge while a mocked-tier failure only shows a red X. Read the Required badges in a PR's
merge box to confirm — it changes without notice and needs repo admin to alter.

## Conventions

- Page objects in `e2e/support/pages/` are shared by both tiers — that is the selector
  contract. Prefer `page.getByTestId(...)` (add `data-testid` attributes to templates) and
  role/label selectors for Material controls.
- Mocked specs import `test` from `../support/test`; live specs import from
  `@playwright/test` directly (no mock fixtures).
