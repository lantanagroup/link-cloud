# Automation.UI

`Automation.UI` is the interactive web host for Link automation runs. It lets users create and
manage test scenarios, configure FHIR query plans, start runs, watch progress in real time,
inspect pipeline state, and review validation outcomes -- all from a browser.

It composes:

- `Automation` (generation + prediction modeling)
- `Automation.Link` (Link-specific orchestration, diagnostics, and validators)
- `LinkSdk` (service API clients)

This README is aimed at three audiences:

- **Product owners / project managers** -- sections 1-4 describe what the UI does and how
  users interact with it.
- **QA** -- sections 5-7 describe the dashboard, run inspection, and the strict
  prediction-vs-actual report surface.
- **Developers** -- sections 8-12 describe the code structure, persistence model, and
  extension points.

---

## 1. What this project is

At a high level, `Automation.UI` is a control plane and observability surface for end-to-end
pipeline tests. It provides:

1. **Scenario management** -- create, edit, clone, and delete reusable test configurations.
2. **Query plan management** -- define which FHIR resource types are acquired during data
   acquisition.
3. **Scenario execution** -- start, cancel, and delete runs.
4. **Live telemetry** -- SignalR-driven logs, status, and dashboard updates.
5. **Dashboard analytics** -- KPIs, success rates, 14-day run histograms.
6. **Persistent run history** -- MongoDB-backed state across restarts.
7. **Snapshot-based inspection** -- pipeline domains pre-aggregated for UI rendering.
8. **Validation-backed confidence** -- report, ABS, DA, normalization, tenant, validation
   checks.

---

## 2. Architecture

```
Automation.UI (ASP.NET Core MVC + Razor views + SignalR)
+-- Controllers/
|   +-- RunsController          dashboard, run lifecycle, snapshot/log APIs
|   +-- ScenariosController     inline CRUD for scenario templates
|   +-- QueryPlansController    inline CRUD for query plan templates
|   +-- HomeController          landing redirect
+-- Views/
|   +-- Runs/Index.cshtml       dashboard: KPIs, run history, quick-launch
|   +-- Runs/Details.cshtml     per-run live status, logs, pipeline snapshots, DA logs
|   +-- Runs/Manifest.cshtml    Generation-Manifest deep dive (generated / predicted / actual)
|   +-- Scenarios/Index.cshtml  scenario list (uses shared editor modal)
|   +-- QueryPlans/Index.cshtml query plan list + inline modal editor
|   +-- Shared/_ScenarioEditorModal.cshtml   reusable scenario editor (markup + JS)
+-- Models/
|   +-- RunDashboardViewModel, RunDashboardStats, TestScenarioDefinition, --
+-- Services/
|   +-- AutomationRunManager    run lifecycle orchestration
|   +-- RunSnapshotOrchestrator background pollers per active run
|   +-- StoreBackedServicePoller polls pipeline domains and writes snapshots
|   +-- RunHub                  SignalR hub for run logs + dashboard updates
|   +-- ScenarioSeedService     seeds system scenarios at startup
+-- Services/Persistence/
    +-- MongoSnapshotStore      run summaries, logs, per-domain snapshots
    +-- MongoScenarioStore      saved scenario templates
    +-- MongoQueryPlanTemplateStore
    +-- MongoIndexManager       Cosmos/Mongo-compatible index initialization
```

The design intentionally separates:

- **Execution path** (`AutomationRunManager`) from
- **Read path** (`ISnapshotStore` + poller snapshots),

so UI reads are fast and never call backend services synchronously.

---

## 3. Controllers and user-facing workflows

### 3.1 `RunsController`

Primary execution surface. Provides the dashboard, run start/cancel/delete, and inspection
APIs.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Dashboard page (KPIs, active runs, recent history, quick-launch). |
| `DashboardStats` | GET | Returns `RunDashboardStats` JSON for AJAX refresh. |
| `Start` | POST | Start a new automation run from a scenario configuration. |
| `Details` | GET | Per-run detail page with live logs and pipeline state. |
| `Manifest` | GET | Generation-Manifest deep-dive page (generated/predicted/actual analysis). |
| `ManifestData` | GET | JSON snapshot of the generation manifest for UI rendering. |
| `AbsUploadData` | GET | JSON ABS upload snapshot for the Generated-vs-ABS comparison. |
| `Status` | GET | JSON run status for polling. |
| `Cancel` / `CancelJson` | POST | Cancel a running automation run. |
| `Delete` / `DeleteJson` | POST | Delete a run and its artifacts. |
| `PipelineSnapshot` | GET | Full diagnostic pipeline snapshot for a run. |
| `DataAcquisitionLogs` | GET | Paginated, sortable DA query logs for a run. |
| `DataAcquisitionLogDetail` | GET | Individual DA log entry detail. |

The dashboard's **quick-launch** dropdown is populated from saved scenarios. Clicking the
**+ New Scenario** button on the Runs page opens the shared scenario editor modal inline -- a
saved scenario is automatically appended to the dropdown and selected, so the user can run the
newly authored scenario without navigating away.

### 3.2 `ScenariosController`

Manages saved test scenario templates. All editing is inline via JSON endpoints -- there are
no separate Create/Edit/Details pages.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Scenario list page (uses shared editor modal). |
| `GetJson` | GET | Return a single scenario as JSON. |
| `SaveInline` | POST | Create or update a scenario (JSON body). |
| `CloneInline` | POST | Clone an existing scenario. |
| `DeleteInline` | POST | Delete a non-system scenario. |

System scenarios (seeded at startup by `ScenarioSeedService`) cannot be modified or deleted,
only cloned. Attempting to modify a system scenario returns `403 Forbidden`.

### 3.3 `QueryPlansController`

Manages FHIR query plan templates that define which resource types are acquired during data
acquisition. Follows the same inline editing pattern as Scenarios.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Query plan list page with inline modal editor. |
| `GetJson` | GET | Return a single query plan as JSON. |
| `GetDefaults` | GET | Return a new query plan pre-populated with defaults. |
| `AcquiredTypes` | GET | Return the list of acquired resource types for a plan. |
| `SaveInline` | POST | Create or update a query plan (JSON body). |
| `CloneInline` | POST | Clone an existing query plan. |
| `DeleteInline` | POST | Delete a non-system query plan. |
| `SetDefaultInline` | POST | Mark a query plan as the system default. |

System query plans cannot be modified or deleted. The default query plan is used for new
scenarios unless overridden.

### 3.4 `HomeController`

Redirects to `Runs/Index`.

---

## 4. View architecture

All management views follow a **single-page inline editing** pattern:

1. **List table** -- shows all items with summary columns.
2. **Modal editor** -- opens over the list for create/edit/view.
3. **IIFE script block** -- self-contained JavaScript at the bottom of the page.
4. **JSON AJAX** -- all mutations use `fetch()` POST with `Content-Type: application/json` and
   the antiforgery token header.

URLs for AJAX endpoints are generated server-side via `@Url.Action("Action", "Controller")`
and embedded as JavaScript variables.

### Shared scenario editor modal

`Views/Shared/_ScenarioEditorModal.cshtml` is a reusable partial that contains both the modal
markup and its self-contained JavaScript IIFE. Any page can embed it with:

```razor
@await Html.PartialAsync("_ScenarioEditorModal")
```

The partial exposes a small public JavaScript API:

```js
// Open the editor for a new scenario
window.openScenarioEditor(null, 'edit');

// Open an existing scenario for edit or read-only view
window.openScenarioEditor(scenarioId, 'edit' | 'view');
```

And dispatches a DOM event on `document` after any successful server action:

```js
document.addEventListener('scenario-editor:changed', function (e) {
    // e.detail === { action: 'saved' | 'cloned' | 'deleted', id, name }
});
```

The Scenarios page reloads on this event; the Runs page appends the saved scenario to the
quick-launch dropdown, selects it, and fires the dropdown's `change` event so the Run button
becomes ready immediately.

### Antiforgery

All POST endpoints use `[ValidateAntiForgeryToken]`. The token is rendered via
`@Html.AntiForgeryToken()` inside the modal and sent as a `RequestVerificationToken` request
header.

---

## 5. Scenario configuration model

A `TestScenarioDefinition` captures everything needed to run a test:

| Field | Purpose |
|---|---|
| `Name` / `Description` | User-facing identification. |
| `IsSystemScenario` | If `true`, seeded by the system and immutable. |
| `ReportMethod` | `Adhoc`, `ScheduledReport`, or `RegenerateReport`. |
| `SelectedMeasures` | Which profiled measures to test. |
| `Seed` | Deterministic generation seed. |
| `PatientCount` | Computed from cohorts. |
| `ResourcesPerPatientMin/Max` | Resource count range per patient. |
| `PatientCohorts` | List of cohort definitions (count, eligibility, clinical profiles, resource range). |
| `QueryPlanTemplateId` | Optional override for the FHIR query plan (null = system default). |
| `CleanupServiceData` | Remove facility config and run artifacts after completion. |
| `CleanupFhirData` | Expunge FHIR server data after completion. |

### System scenarios

`ScenarioSeedService` seeds a set of canonical system scenarios at startup. Each one mirrors a
corresponding backend E2E test (same seed, patient count, resource range, prefix), so running
the UI scenario and the backend test produces bit-for-bit identical FHIR input:

| System scenario | Seed | Patients | Resources |
|---|---:|---:|---:|
| Adhoc Report Test | 20260326 | 1 | 1000 |
| Multi Patient Test | 20260328 | 150 | 25-50 |
| Mega Patient Test | 20260327 | 1 | 5000 |
| Mega Multi Patient Test | 20260330 | 150 | 5000 / 25-50 |
| Scheduled Report Test | 20260326 | 1 | 1000 |
| Regenerate Report Test | 20260401 | 1 | 100 |
| Multi Measure Test | 20260420 | 2 | 250 |

These scenarios match `Tests/BackendE2ETests` entry by entry (see that project's README).

### Cohort model

Each cohort defines a group of patients sharing:

- **Patient count** -- how many patients to generate.
- **Measure eligibility** -- per-measure qualifying / non-qualifying checkboxes.
- **Clinical profiles** -- which of the 16 clinical scenarios to draw from (filtered by
  eligibility).
- **Resource range** -- min/max resources per patient.

The editor enforces measure eligibility constraints in the UI:

- ACH Monthly and Daily measures always qualify for every inpatient encounter, so the
  qualifying checkbox is always checked (non-qualifying ACH is not currently meaningful at
  the generation layer).
- The Hypoglycemic measure requires ACH Monthly to also be qualifying (dependency
  enforcement).

---

## 6. Dashboard and real-time updates

### 6.1 Dashboard stats

`RunDashboardStats` provides aggregated KPIs computed from the last 14 days of run history:

- Total runs, succeeded, failed, cancelled, running, queued counts.
- Success rate percentage.
- Average duration for completed runs.
- 14-day histogram with per-day succeeded/failed/cancelled buckets.

### 6.2 SignalR hub

`RunHub` supports two subscription groups:

| Method | Group | Purpose |
|---|---|---|
| `SubscribeRun(runId)` | Per-run group | Receives `log` and `status` events for a specific run. |
| `SubscribeDashboard()` | `dashboard` group | Receives `dashboardUpdate` events when any run changes status. |

`AutomationRunManager` broadcasts to both groups on every status transition, enabling live log
streaming on the run details page and real-time dashboard KPI updates without polling.

---

## 7. End-to-end run workflow

`AutomationRunManager.ExecuteAsync(...)` coordinates the full lifecycle:

1. **Resolve request into run options**
   - Scenario defaults + custom overrides.
   - Selected measures.
   - Patient cohorts/profiles expansion via `PatientCohortDefinition.ExpandProfiles()`.
2. **Generate FHIR input**
   - `FhirGenerationPipeline.GenerateAndUploadAsync()` for streaming generation.
   - `GenerationManifest` is built incrementally during generation.
   - `QueryPlanAcquisitionSimulator` runs per-patient.
   - `CqlFilterSimulator` runs per-patient over the patient's qualifying measures only.
3. **Initialize validation dependencies** -- validation artifacts and categories.
4. **Load measure bundles** -- `MeasureLoader.LoadAllAsync()` (supports multi-measure).
5. **Ensure tenant/pipeline setup** -- facility, normalization config, query plans/config,
   query dispatch config.
6. **Generate report and monitor submission**
   - Start `BackgroundDiagnosticsMonitor`.
   - Poll until submission / critical failure / timeout.
7. **(Optional) regeneration path** -- regenerate report and switch pollers to the new report
   ID.
8. **Snapshot + artifact download + validation suite**
   - Full diagnostic snapshot.
   - External/internal ABS downloads.
   - Validators run via `RunValidator` wrapper with per-validator error isolation.
   - Partial `validatorResults` persisted after each validator completes.
9. **Cleanup** -- service-level and FHIR cleanup based on run config.

### Validation orchestration

Validators are wrapped in a `RunValidator` helper that:

- Catches exceptions per-validator so one failure doesn't prevent subsequent validators.
- Persists partial `validatorResults` snapshots after each step.
- Ensures partial results are visible in the dashboard even if a later validator throws.

Run status transitions are persisted and broadcast in real time via SignalR.

---

## 8. Predictive ABS/resource validation model

For profile-based runs the UI host builds a deterministic `GenerationManifest` and enriches it
with:

- Acquired resource types from the effective query plan.
- Parameter-query resource types.
- Simulated acquired keys per patient (`QueryPlanAcquisitionSimulator`).
- CQL-referenced resource types (`CqlResourceTypeExtractor`).
- Per-resource CQL exclusions (`CqlFilterSimulator`, measure-family profiles, intersection
  semantics across the patient's qualifying measures).

That manifest is passed to `ReportAbsManifestValidator` and `ReportDatabaseValidator` for
strict prediction-vs-actual comparison. The Report service's `ReportEntry.ReportingStatus`
rows feed the `OperationOutcome` count prediction (one OO per patient with
`FailedValidation`).

See `DotNet/Automation/README.md` section 7 for the full prediction formula.

### Generation Manifest page

`Runs/Manifest` provides a deep-dive UX for this model:

- **Overview tab** -- measure context, query-plan acquired types, CQL-referenced types,
  generated resource distribution (patient + shared).
- **Patients tab** -- per-patient generated counts and type badges.
- **Generated vs ABS tab** -- side-by-side prediction and actual ABS upload counts, per-type
  and per-patient deltas, pipeline explanation distinguishing type-level CQL reachability
  from resource-level SDE filtering, and explicit rows for pipeline-derived types (Patient,
  MeasureReport, OperationOutcome) whose counts are predicted deterministically.

The page is intended to answer "what did we generate?", "what did we predict?", and "what did
ABS actually contain?" with enough fidelity to debug drift quickly.

---

## 9. Core services

### `AutomationRunManager`

Primary orchestrator. Owns run state, cancellation workflow, per-run DI container creation,
validator invocation, and SignalR log/status broadcasts. Provides `GetDashboardStatsAsync()`
for the dashboard (queries the last 14 days of runs).

### `RunSnapshotOrchestrator`

Background service that reconciles active runs from persistence and ensures each has exactly
one poller:

- Starts pollers for newly active runs.
- Restarts pollers when run context changes (e.g., regenerated report ID).
- Performs a final poll on completion to flush terminal domain state.

### `StoreBackedServicePoller`

Per-run poller with one cadence for all key domains:

- `schedule`
- `entries`
- `populations`
- `acquisitionSummary`
- `measureResources`
- `validationResources`

Each domain write is independent and fault-isolated. Failures are logged to run logs without
taking down the whole poll cycle.

### `ISnapshotStore` + Mongo stores

Persistence abstraction used by both execution and UI reads:

- Run metadata and summaries.
- Live + persisted log lines.
- Per-domain snapshot payloads.
- `GetAllRunSummariesAsync(since)` supports date filtering to avoid unbounded collection
  scans.

This enables restart-safe run history and multi-instance read sharing.

---

## 10. Persistence model (MongoDB / Cosmos Mongo API)

Collections:

- `automation_runs` -- run summaries and status metadata.
- `automation_snapshots` -- per-run, per-domain snapshot payloads.
- `automation_logs` -- full run logs.
- `automation_scenarios` -- user and system scenario templates.
- `automation_query_plan_templates` -- query plan templates.

### Document conventions

- All `Guid` primary keys use `[BsonId] [BsonRepresentation(BsonType.String)]`.
- All `Guid?` foreign keys (e.g., `QueryPlanTemplateId`) use
  `[BsonRepresentation(BsonType.String)]`.
- Complex nested objects (patient profiles, cohorts, query entries) are stored as serialized
  JSON strings to avoid BSON schema rigidity.

### `MongoIndexManager`

Initializes required indexes with Cosmos-safe behavior:

- Checks key-shape existence first.
- Avoids modifying incompatible existing unique indexes.
- Logs failures without blocking startup.

---

## 11. Authentication model

### Inbound (browser -- Automation.UI)

External authentication is handled at the infrastructure layer (domain-level OAuth2 via
reverse proxy or gateway). The application itself does not enforce inbound authentication --
all authorization policies are pass-through:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});
```

### Outbound (Automation.UI -- Link services)

- `Authentication:UseBearerForServiceCalls=true` (recommended) -- LinkSdk attaches Link bearer
  tokens via `ICreateSystemToken`.
- `Authentication:UseBearerForServiceCalls=false` -- outbound calls are anonymous.

Token generation settings come from `LinkTokenService` configuration (not user tokens).

---

## 12. Configuration

Primary configuration sources:

- `appsettings.json`
- `appsettings.Docker.json`
- Environment variables
- Optional Azure App Configuration + Key Vault references

Key sections:

| Section | Purpose |
|---|---|
| `Automation` | Runtime orchestration settings (FHIR / AdminBFF / Loki / query behavior). |
| `ServiceRegistry` | Base URLs for Link services used by `LinkSdk` clients. |
| `MongoDB` | Connection/database for UI persistence. |
| `Authentication` | Outbound bearer toggle. |
| `LinkTokenService` | System-token signing/issuer settings for outbound service calls. |

---

## 13. Running

### Local

```bash
dotnet run --project DotNet/Automation.UI/Automation.UI.csproj
```

### Docker

```bash
docker compose up automation-ui
```

Port mapping: host `5256` -- container `5257`.

---

## 14. Notes

- Targets `.NET 8`.
- MVC + Razor views + SignalR (not Razor Pages `PageModel` routing, despite the workspace
  characteristics hint -- the project uses classic MVC controllers and `@Html.PartialAsync`).
- `RunHub.SubscribeRun(runId)` groups clients for per-run log streaming;
  `RunHub.SubscribeDashboard()` groups clients for dashboard-wide status updates.
- The read path is store-backed by design; backend service polling is centralized in
  background pollers.
- All management views use a single-page inline modal editing pattern -- no multi-page form
  workflows. The scenario editor is a single reusable partial shared between the Scenarios
  and Runs pages.
- System scenarios and system query plans are seeded at startup and cannot be modified, only
  cloned.
- System scenarios are kept in sync with `Tests/BackendE2ETests` so that UI and test
  executions are reproducible from either host.
