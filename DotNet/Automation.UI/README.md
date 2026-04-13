# Automation.UI

`Automation.UI` is the interactive web host for Link automation runs. It lets users start scenarios, watch run progress in real time, inspect pipeline state, and review validation outcomes without using a test runner directly.

It composes:

- `Automation` (generation + expectation modeling)
- `Automation.Link` (Link-specific orchestration, diagnostics, and validators)
- `LinkSdk` (service API clients)

---

## What this project is

At a high level, `Automation.UI` is a control plane and observability surface for end-to-end pipeline tests.

It provides:

1. **Scenario execution** (start, cancel, delete runs)
2. **Live telemetry** (SignalR logs + status)
3. **Persistent run history** (Mongo-backed state across restarts)
4. **Snapshot-based inspection** (pipeline domains pre-aggregated for UI)
5. **Validation-backed confidence** (report, ABS, DA, normalization, tenant, validation checks)

---

## Architecture

```
Automation.UI (ASP.NET Core MVC + Razor views + SignalR)
|-- Controllers/                 - HTTP endpoints and view composition
|-- Services/
|   |-- AutomationRunManager     - run lifecycle orchestration
|   |-- RunSnapshotOrchestrator  - manages background pollers per active run
|   |-- StoreBackedServicePoller - polls pipeline domains and writes snapshots
|   `-- RunHub                   - SignalR stream for run logs
`-- Services/Persistence/
    |-- MongoSnapshotStore       - run summaries, logs, per-domain snapshots
    |-- MongoScenarioStore       - saved scenario templates
    |-- MongoQueryPlanTemplateStore
    `-- MongoIndexManager        - Cosmos/Mongo-compatible index initialization
```

The design intentionally separates:

- **Execution path** (`AutomationRunManager`) from
- **Read path** (`ISnapshotStore` + poller snapshots)

so UI reads are fast and do not call backend services directly.

---

## End-to-end run workflow

`AutomationRunManager.ExecuteAsync(...)` coordinates the full lifecycle:

1. **Resolve request into run options**
   - scenario defaults + custom overrides
   - selected measures
   - patient cohorts/profiles expansion
2. **Generate FHIR input**
   - uses `FhirBundleGenerator` (profile-driven when provided)
3. **Load generated bundles**
   - waits for FHIR readiness, uploads transaction bundles
4. **Initialize validation dependencies**
   - validation artifacts/categories
5. **Load measure bundles**
   - via `MeasureLoader.LoadAllAsync()` (supports multi-measure)
6. **Ensure tenant/pipeline setup**
   - facility, normalization config, query plans/config, query dispatch config
7. **Generate report and monitor submission**
   - starts `BackgroundDiagnosticsMonitor`
   - polls until submission/critical failure/timeout
8. **(Optional) regeneration path**
   - regenerates report and switches pollers to the new report ID
9. **Snapshot + artifact download + validation suite**
   - full diagnostic snapshot
   - external/internal ABS downloads
   - validators: report DB, ABS manifest, DA DB, normalization DB, tenant DB, validation results
10. **Cleanup**
    - service-level and FHIR cleanup based on run config

Run status transitions are persisted and broadcast in real time.

---

## Core object/process relationships

### `AutomationRunManager`

Primary orchestrator. Owns run state, cancellation workflow, per-run DI container creation, validator invocation, and SignalR log/status broadcasts.

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

This enables restart-safe run history and multi-instance read sharing.

---

## Predictive ABS/resource validation model

For profile-based runs, the UI host builds a deterministic `GenerationManifest` and enriches it with:

- acquired resource types from effective query plan
- parameter-query resource types
- simulated acquired keys per patient (`QueryPlanAcquisitionSimulator`)
- CQL-referenced resource types (`CqlResourceTypeExtractor`)

That manifest is passed to `ReportAbsManifestValidator` and `ReportDatabaseValidator` to compare predicted vs actual output.

This yields expectation checks driven by generated inputs and query/CQL semantics instead of static baselines.

---

## Controllers and user-facing workflows

| Controller | Purpose |
|---|---|
| `RunsController` | Start/cancel/delete runs, run list/details/status, snapshot/API utilities |
| `ScenariosController` | Manage saved run scenario templates |
| `QueryPlansController` | Manage saved query plan templates and defaults |
| `AccountController` | Login/logout/access-denied endpoints |
| `HomeController` | Landing page |

`RunsController` is the primary execution surface. Scenario/query-plan controllers provide reusable templates that feed run start requests.

---

## Persistence model (MongoDB / Cosmos Mongo API)

Collections:

- `automation_runs` - run summaries and status metadata
- `automation_snapshots` - per-run, per-domain snapshot payloads
- `automation_logs` - full run logs
- `automation_scenarios` - user/system scenario templates
- `automation_query_plan_templates` - query plan templates

`MongoIndexManager` initializes required indexes with Cosmos-safe behavior:

- checks key-shape existence first
- avoids modifying incompatible existing unique indexes
- logs failures without blocking startup

---

## Authentication model

Inbound UI access and outbound service auth are intentionally decoupled.

### Inbound (browser -> Automation.UI)

- `Authentication:EnableAnonymousAccess=true` -> UI auth bypass
- `Authentication:EnableAnonymousAccess=false` -> OIDC cookie/session flow

OIDC keys:

- `Authentication:Schemas:OpenIdConnect:Authority`
- `Authentication:Schemas:OpenIdConnect:ClientId`
- `Authentication:Schemas:OpenIdConnect:ClientSecret` (if required)
- `Authentication:Schemas:OpenIdConnect:CallbackPath` (default `/signin-oidc`)

### Outbound (Automation.UI -> Link services)

- `Authentication:UseBearerForServiceCalls=true` (recommended)
  - LinkSdk attaches Link bearer tokens
- `Authentication:UseBearerForServiceCalls=false`
  - outbound calls are anonymous

Token generation settings come from `LinkTokenService` (not user OIDC tokens).

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
| `Authentication` | inbound UI auth + outbound bearer toggle |
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

---

## Notes

- Targets `.NET 8`.
- MVC + Razor views + SignalR (not Razor Pages `PageModel` routing).
- `RunHub.SubscribeRun(runId)` groups clients for per-run log streaming.
- Read path is store-backed by design; backend service polling is centralized in background pollers.
