
`Automation.UI` is the interactive web host for Link automation runs. It lets users create and manage test scenarios, configure FHIR query plans, start runs, watch run progress in real time, inspect pipeline state, and review validation outcomes — all from a browser.

It composes:

- `Automation` (generation + expectation modeling)
- `Automation.Link` (Link-specific orchestration, diagnostics, and validators)
- `LinkSdk` (service API clients)

---

## What this project is

At a high level, `Automation.UI` is a control plane and observability surface for end-to-end pipeline tests.

It provides:

1. **Scenario management** (create, edit, clone, delete reusable test configurations)
2. **Query plan management** (define which FHIR resource types are acquired during data acquisition)
3. **Scenario execution** (start, cancel, delete runs)
4. **Live telemetry** (SignalR logs + status + dashboard updates)
5. **Dashboard analytics** (KPIs, success rates, 14-day run histograms)
6. **Persistent run history** (Mongo-backed state across restarts)
7. **Snapshot-based inspection** (pipeline domains pre-aggregated for UI)
8. **Validation-backed confidence** (report, ABS, DA, normalization, tenant, validation checks)

---

## Architecture

```
Automation.UI (ASP.NET Core MVC + Razor views + SignalR)
├── Controllers/
│  ├── RunsController          — dashboard, run lifecycle, snapshot/log APIs
│  ├── ScenariosController     — inline CRUD for scenario templates
│  ├── QueryPlansController    — inline CRUD for query plan templates
│  └── HomeController          — landing redirect
├── Views/
│  ├── Runs/Index.cshtml       — dashboard with KPI cards, run history, quick-launch
│  ├── Runs/Details.cshtml     — per-run live status, logs, pipeline snapshots, DA logs
│  ├── Scenarios/Index.cshtml  — scenario list + inline modal editor
│  └── QueryPlans/Index.cshtml — query plan list + inline modal editor
├── Models/
│  ├── RunDashboardViewModel   — composed view model for dashboard page
│  ├── RunDashboardStats       — aggregated KPIs (totals, success rate, histogram)
│  └── TestScenarioDefinition  — full scenario configuration model
├── Services/
│  ├── AutomationRunManager    — run lifecycle orchestration
│  ├── RunSnapshotOrchestrator — manages background pollers per active run
│  ├── StoreBackedServicePoller— polls pipeline domains and writes snapshots
│  └── RunHub                  — SignalR hub for run logs + dashboard updates
└── Services/Persistence/
   ├── MongoSnapshotStore      — run summaries, logs, per-domain snapshots
   ├── MongoScenarioStore      — saved scenario templates
   ├── MongoQueryPlanTemplateStore
   └── MongoIndexManager       — Cosmos/Mongo-compatible index initialization
```

The design intentionally separates:

- **Execution path** (`AutomationRunManager`) from
- **Read path** (`ISnapshotStore` + poller snapshots)

so UI reads are fast and do not call backend services directly.

---

## Controllers and user-facing workflows

### `RunsController`

Primary execution surface. Provides the dashboard, run start/cancel/delete, and inspection APIs.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Dashboard page with KPI stats, active runs, recent history, quick-launch |
| `DashboardStats` | GET | Returns `RunDashboardStats` JSON for AJAX refresh |
| `Start` | POST | Start a new automation run from a scenario configuration |
| `Details` | GET | Per-run detail page with live logs and pipeline state |
| `Manifest` | GET | Generation Manifest deep-dive page for generated/predicted/actual analysis |
| `ManifestData` | GET | JSON generation manifest snapshot for UI rendering |
| `AbsUploadData` | GET | JSON ABS upload snapshot for Generated vs ABS comparison |
| `Status` | GET | JSON run status for polling |
| `Cancel` / `CancelJson` | POST | Cancel a running automation run |
| `Delete` / `DeleteJson` | POST | Delete a run and its artifacts |
| `PipelineSnapshot` | GET | Full diagnostic pipeline snapshot for a run |
| `DataAcquisitionLogs` | GET | Paginated, sortable DA query logs for a run |
| `DataAcquisitionLogDetail` | GET | Individual DA log entry detail |

### `ScenariosController`

Manages saved test scenario templates. All editing is inline via JSON endpoints — there are no separate Create/Edit/Details pages.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Scenario list page with inline modal editor |
| `GetJson` | GET | Return a single scenario as JSON |
| `SaveInline` | POST | Create or update a scenario (JSON body) |
| `CloneInline` | POST | Clone an existing scenario |
| `DeleteInline` | POST | Delete a non-system scenario |

System scenarios (seeded at startup by `ScenarioSeedService`) cannot be modified or deleted, only cloned. Attempting to modify a system scenario returns `403 Forbidden`.

### `QueryPlansController`

Manages FHIR query plan templates that define which resource types are acquired during data acquisition. Follows the same inline editing pattern as Scenarios.

| Action | Method | Purpose |
|---|---|---|
| `Index` | GET | Query plan list page with inline modal editor |
| `GetJson` | GET | Return a single query plan as JSON |
| `GetDefaults` | GET | Return a new query plan pre-populated with defaults |
| `AcquiredTypes` | GET | Return the list of acquired resource types for a plan |
| `SaveInline` | POST | Create or update a query plan (JSON body) |
| `CloneInline` | POST | Clone an existing query plan |
| `DeleteInline` | POST | Delete a non-system query plan |
| `SetDefaultInline` | POST | Mark a query plan as the system default |

System query plans cannot be modified or deleted. The default query plan is automatically used for new scenarios unless overridden.

### `HomeController`

Redirects to `Runs/Index`.

---

## View architecture

All management views follow a **single-page inline editing** pattern:

1. **List table** — shows all items with summary columns
2. **Modal editor** — opens over the list for create/edit/view
3. **IIFE script block** — self-contained JavaScript at the bottom of the page
4. **JSON AJAX** — all mutations use `fetch()` POST with `Content-Type: application/json` and antiforgery token header

URLs for AJAX endpoints are generated server-side via `@Url.Action("ActionName", "ControllerName")` and embedded as JavaScript variables.

This pattern eliminates multi-page form workflows, partial views, and separate Create/Edit/Details pages. Each entity type (Scenarios, Query Plans) has exactly one `.cshtml` file.

### Antiforgery

All POST endpoints use `[ValidateAntiForgeryToken]`. The token is rendered via `@Html.AntiForgeryToken()` inside the modal and sent as a `RequestVerificationToken` request header.

---

## Scenario configuration model

A `TestScenarioDefinition` captures everything needed to run a test:

| Field | Purpose |
|---|---|
| `Name` / `Description` | User-facing identification |
| `IsSystemScenario` | If true, seeded by system and immutable |
| `ReportMethod` | `Adhoc`, `ScheduledReport`, or `RegenerateReport` |
| `SelectedMeasures` | Which profiled measures to test |
| `Seed` | Deterministic generation seed |
| `PatientCount` | Computed from cohorts |
| `ResourcesPerPatientMin/Max` | Resource count range per patient |
| `PatientPrefix` | FHIR Patient ID prefix |
| `PatientCohorts` | List of cohort definitions (count, eligibility, clinical profiles, resource range) |
| `QueryPlanTemplateId` | Optional override for FHIR query plan (null = system default) |
| `CleanupServiceData` | Remove facility config and run artifacts after completion |
| `CleanupFhirData` | Expunge FHIR server data after completion |

### Cohort model

Each cohort defines a group of patients with shared characteristics:

- **Patient count** — how many patients to generate
- **Measure eligibility** — per-measure qualifying/non-qualifying checkboxes
- **Clinical profiles** — which of the 16 clinical scenarios to draw from (filtered by eligibility)
- **Resource range** — min/max resources per patient

The UI enforces measure eligibility constraints:
- ACH Monthly and Daily measures always qualify for all clinical scenarios, so the qualifying checkbox is always checked (non-qualifying ACH is not currently supported by the generation layer)
- Hypoglycemic measure requires ACH Monthly to also be qualifying (dependency enforcement)

---

## Dashboard and real-time updates

### Dashboard stats model

`RunDashboardStats` provides aggregated KPIs computed from the last 14 days of run history:

- Total runs, succeeded, failed, cancelled, running, queued counts
- Success rate percentage
- Average duration for completed runs
- 14-day histogram with per-day succeeded/failed/cancelled buckets

### SignalR hub

`RunHub` supports two subscription groups:

| Method | Group | Purpose |
|---|---|---|
| `SubscribeRun(runId)` | Per-run group | Receives `log` and `status` events for a specific run |
| `SubscribeDashboard()` | `dashboard` group | Receives `dashboardUpdate` events when any run changes status |

The `AutomationRunManager` broadcasts to both groups on every status transition, enabling:
- Live log streaming on the run details page
- Real-time dashboard KPI updates without polling

---

## End-to-end run workflow

`AutomationRunManager.ExecuteAsync(...)` coordinates the full lifecycle:

1. **Resolve request into run options**
   - scenario defaults + custom overrides
   - selected measures
   - patient cohorts/profiles expansion via `PatientCohortDefinition.ExpandProfiles()`
2. **Generate FHIR input**
   - uses `FhirGenerationPipeline.GenerateAndUploadAsync()` for streaming generation
   - builds `GenerationManifest` incrementally during generation
   - runs `QueryPlanAcquisitionSimulator` per-patient (when configured)
3. **Initialize validation dependencies**
   - validation artifacts/categories
4. **Load measure bundles**
   - via `MeasureLoader.LoadAllAsync()` (supports multi-measure)
5. **Ensure tenant/pipeline setup**
   - facility, normalization config, query plans/config, query dispatch config
6. **Generate report and monitor submission**
   - starts `BackgroundDiagnosticsMonitor`
   - polls until submission/critical failure/timeout
7. **(Optional) regeneration path**
   - regenerates report and switches pollers to the new report ID
8. **Snapshot + artifact download + validation suite**
   - full diagnostic snapshot
   - external/internal ABS downloads
   - validators run via `RunValidator` wrapper with per-validator error isolation
   - partial `validatorResults` persisted after each validator completes
9. **Cleanup**
   - service-level and FHIR cleanup based on run config

### Validation orchestration

Validators are wrapped in a `RunValidator` helper that:
- Catches exceptions per-validator so one failure doesn't prevent subsequent validators
- Persists partial `validatorResults` snapshots after each step
- Enables partial results to be visible in the dashboard even if a later validator throws

Run status transitions are persisted and broadcast in real time via SignalR.

---

## Core service relationships

### `AutomationRunManager`

Primary orchestrator. Owns run state, cancellation workflow, per-run DI container creation, validator invocation, and SignalR log/status broadcasts. Also provides `GetDashboardStatsAsync()` for the dashboard (queries last 14 days of runs).

### `RunSnapshotOrchestrator`

Background service that reconciles active runs from persistence and ensures each has exactly one poller.

- starts pollers for newly active runs
- restarts pollers when run context changes (e.g., regenerated report ID)
- performs final poll on completion to flush terminal domain state

### `StoreBackedServicePoller`

Per-run poller with one cadence for all key domains:

- `schedule`
- `entries`
- `populations`
- `acquisitionSummary`
- `measureResources`
- `validationResources`

Each domain write is independent and fault-isolated. Failures are logged to run logs without taking down the whole poll cycle.

### `ISnapshotStore` + Mongo stores

Persistence abstraction used by both execution and UI reads:

- run metadata and summaries
- live + persisted log lines
- per-domain snapshot payloads
- `GetAllRunSummariesAsync(since)` supports date filtering to avoid unbounded collection scans

This enables restart-safe run history and multi-instance read sharing.

---

## Persistence model (MongoDB / Cosmos Mongo API)

Collections:

- `automation_runs` — run summaries and status metadata
- `automation_snapshots` — per-run, per-domain snapshot payloads
- `automation_logs` — full run logs
- `automation_scenarios` — user/system scenario templates
- `automation_query_plan_templates` — query plan templates

### Document conventions

- All `Guid` primary keys use `[BsonId] [BsonRepresentation(BsonType.String)]`
- All `Guid?` foreign keys (e.g., `QueryPlanTemplateId`) use `[BsonRepresentation(BsonType.String)]`
- Complex nested objects (patient profiles, cohorts, query entries) are stored as serialized JSON strings to avoid BSON schema rigidity

### `MongoIndexManager`

Initializes required indexes with Cosmos-safe behavior:

- checks key-shape existence first
- avoids modifying incompatible existing unique indexes
- logs failures without blocking startup

---

## Predictive ABS/resource validation model

For profile-based runs, the UI host builds a deterministic `GenerationManifest` and enriches it with:

- acquired resource types from effective query plan
- parameter-query resource types
- simulated acquired keys per patient (`QueryPlanAcquisitionSimulator`)
- CQL-referenced resource types (`CqlResourceTypeExtractor`)
- per-resource CQL exclusions (`CqlFilterSimulator`, measure-family profiles)

That manifest is passed to `ReportAbsManifestValidator` and `ReportDatabaseValidator` to compare predicted vs actual output.

This yields expectation checks driven by generated inputs and query/CQL semantics instead of static baselines.

### Generation Manifest page

`Runs/Manifest` provides a deep-dive UX for this model:

- **Overview tab**
  - measure context, query-plan acquired types, CQL-referenced types
  - generated resource distribution (patient + shared)
- **Patients tab**
  - per-patient generated counts and type badges
- **Generated vs ABS tab**
  - side-by-side prediction and actual ABS upload counts
  - per-type and per-patient deltas
  - pipeline explanation that distinguishes:
    - type-level CQL reachability
    - resource-level SDE filtering

This page is intended to answer “what did we generate?”, “what did we predict?”, and “what did ABS actually contain?” with enough fidelity to debug drift quickly.

---

## Authentication model

### Inbound (browser → Automation.UI)

External authentication is handled at the infrastructure layer (domain-level OAuth2 via reverse proxy or gateway). The application itself does not enforce inbound authentication — all authorization policies are pass-through:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
});
```

### Outbound (Automation.UI → Link services)

- `Authentication:UseBearerForServiceCalls=true` (recommended)
  - LinkSdk attaches Link bearer tokens via `ICreateSystemToken`
- `Authentication:UseBearerForServiceCalls=false`
  - outbound calls are anonymous

Token generation settings come from `LinkTokenService` configuration (not user tokens).

---

## Configuration overview

Primary configuration sources:

- `appsettings.json`
- `appsettings.Docker.json`
- environment variables
- optional Azure App Configuration + Key Vault references

Key sections:

| Section | Purpose |
|---|---|
| `Automation` | runtime orchestration settings (FHIR/AdminBFF/Loki/query behavior) |
| `ServiceRegistry` | base URLs for Link services used by `LinkSdk` clients |
| `MongoDB` | connection/database for UI persistence |
| `Authentication` | outbound bearer toggle |
| `LinkTokenService` | system-token signing/issuer settings for outbound service calls |

---

## Running

### Local

```bash
dotnet run --project DotNet/Automation.UI/Automation.UI.csproj
```

### Docker

```bash
docker compose up automation-ui
```

Port mapping: host `5256` → container `5257`.

---

## Notes

- Targets `.NET 8`.
- MVC + Razor views + SignalR (not Razor Pages `PageModel` routing).
- `RunHub.SubscribeRun(runId)` groups clients for per-run log streaming.
- `RunHub.SubscribeDashboard()` groups clients for dashboard-wide status updates.
- Read path is store-backed by design; backend service polling is centralized in background pollers.
- All management views use a single-page inline modal editing pattern — no multi-page form workflows.
- System scenarios and system query plans are seeded at startup and cannot be modified, only cloned.
