# BackendE2ETests

Backend E2E tests are **orchestration/contract tests** over Link.UI APIs, not a second
copy of full pipeline orchestration logic.

The suite verifies that:

1. Link.UI can start seeded system scenarios via API,
2. scenario runs reach terminal `Succeeded` status,
3. API Health (`Run All`) can be started and summarized via API,
4. a PR-safe subset of backend scenarios is exercised in CI.

---

## 1. What changed

Historically, this project duplicated large amounts of report-pipeline setup and validation logic
directly in each test class. That behavior now lives in Link.UI system
scenarios and API Health suites.

`BackendE2ETests` acts as a thin API-driven verifier against Link.UI:

- scenario tests call `/api/runs/*`,
- API stability calls `/api/api-health-runs/*`,
- the smoke test reads the antiforgery token from `GET /Runs` before it posts.
- scenario run pages are `/Automation/Runs/{id}`. The tests poll `/api/runs/{id}/status` and do not open that page.

---

## 2. Test suites

### Scenario-backed tests (`/api/runs`)

These tests use `AutomationUiScenarioRunner` to start a seeded scenario and poll until terminal.

| Suite | System scenario id | Notes |
|---|---|---|
| `AdhocReportTest` | `00000000-0000-0000-0000-000000000001` | PR-safe (ACH Monthly) |
| `AdhocReportDailyAchTest` | `00000000-0000-0000-0000-000000000009` | PR-safe (ACH Daily) |
| `MultiPatientTest` | `00000000-0000-0000-0000-000000000002` | PR-safe |
| `MegaPatientTest` | `00000000-0000-0000-0000-000000000003` | `Category=LongRunning` |
| `ReportScheduledWorkflowTest` (`ReportScheduledTest`) | `00000000-0000-0000-0000-000000000004` | PR-safe |
| `RegenerateReportTest` | `00000000-0000-0000-0000-000000000005` | PR-safe |
| `MultiMeasureTest` | `00000000-0000-0000-0000-000000000006` | PR-safe |
| `MegaMultiPatientTest` | `00000000-0000-0000-0000-000000000007` | `Category=LongRunning` |

### API health / stability tests

| Suite | Endpoint(s) | Notes |
|---|---|---|
| `ApiStabilityTest` | `POST /api/api-health-runs/start-all`, `GET /api/api-health-runs/{runId}/status` | Aggregated API health check across services |
| `AutomationUiApiSmokeTest` | `POST /api/runs/start`, `GET /api/runs/{id}/status` | Validates antiforgery + service-to-service API launch contract |

---

## 3. Test flow

### 3.1 Scenario-backed tests

Each scenario-backed suite does the following:

1. Call `POST /api/runs/start` with a seeded scenario id.
2. Poll `GET /api/runs/{runId}/status` every 10s.
3. Assert terminal state is reached and `Status == Succeeded`.

All pipeline generation, seeding, validation, cleanup, and diagnostics happen inside
`Automation.UI` run execution.

### 3.2 API stability test

`ApiStabilityTest` does the following:

1. Call `POST /api/api-health-runs/start-all`.
2. Poll `GET /api/api-health-runs/{runId}/status`.
3. Assert aggregate run succeeded; log failed endpoint summaries if present.

---

## 4. Running tests

From repository root:

```bash
# Run all Backend E2E tests
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj

# Run PR-safe subset (excludes long-running mega suites)
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter "Category!=LongRunning"

# Run a single category
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter "Category=ApiStabilityTest"
```

---

## 5. CI behavior (PRs)

GitHub workflow `.github/workflows/tests.yaml` intentionally runs a **curated PR subset** of
Backend E2E categories and excludes long-running mega stress tests.

Current PR list includes:

- `AdhocReportTest`
- `AdhocReportDailyAchTest`
- `ApiStabilityTest`
- `AutomationUiSmokeTest`
- `ReportScheduledTest`
- `RegenerateReportTest`
- `MultiMeasureTest`

`MegaPatientTest` and `MegaMultiPatientTest` are marked with `Category=LongRunning` and are not
part of the PR path.

---

## 6. Prerequisites

A full Link stack must be running, including Link.UI and all dependent services.

At minimum, tests require:

- Link.UI reachable at the base URL below
- services behind the scenario execution and API Health workflows (FHIR, Kafka, Mongo, SQL, etc.)

The repository `docker-compose.yml` provides a suitable local environment. The `link-ui` service is published on host port **5258**. `dotnet run` for Link.UI uses **5280** (`Properties/launchSettings.json`). The `automation-ui` service remains on **5256** and is not the default for this suite.

---

## 7. Configuration

Key environment variables used by this project:

| Variable | Purpose | Default |
|---|---|---|
| `LINK_UI_BASE_URL` | Base URL for Link.UI. Wins when it is set. | unset |
| `AUTOMATION_UI_BASE_URL` | Fallback base URL when `LINK_UI_BASE_URL` is unset. | unset |
| (neither set) | `link-ui` compose port | `http://localhost:5258` |
| `AUTOMATION_UI_SMOKE_TIMEOUT_MINUTES` | Timeout for `AutomationUiApiSmokeTest` | `20` |
| `API_STABILITY_TIMEOUT_MINUTES` | Timeout for `ApiStabilityTest` API Health run-all polling | `30` |

These tests do not read a bearer token. `POST /api/runs/start` and `POST /api/api-health-runs/start-all` send the antiforgery token scraped from `GET /Runs`. The Azure pipeline in `Tests/AutomationUI.Pipeline` is the caller that uses `LINK_UI_API_ACCESS_TOKEN` / `AUTOMATION_UI_API_ACCESS_TOKEN` and `LINK_UI_API_AUDIENCE` / `AUTOMATION_UI_API_AUDIENCE`.

Other service URL variables in `TestConfig` still exist for compatibility. The scenario and API Health tests call Link.UI, which then calls those services.

---

## 8. Architecture

```
BackendE2ETests
+-- AutomationUiScenarioRunner  (shared API runner for /api/runs)
+-- ApiStabilityTest            (API Health run-all orchestration)
+-- AutomationUiApiSmokeTest    (antiforgery + API contract smoke test)
```

Primary responsibility is verifying API contracts and orchestration outcomes, not re-implementing
full pipeline internals in test code.

---

## 9. Notes

- Targets `.NET 8`.
- Tests are `[Fact]` classes with `IClassFixture<BackendE2ETestFixture>`.
- Scenario ids are deterministic and seeded by Link.UI `ScenarioSeedService`.
- If a scenario fails, inspect the Link.UI run at `/Automation/Runs/{id}` first, since that host performs
  the underlying generation/validation workflow.
