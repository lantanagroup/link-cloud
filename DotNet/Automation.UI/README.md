# Automation.UI

`Automation.UI` is an ASP.NET Core Razor Pages web application that provides an interactive UI for running, monitoring, and inspecting Link automation test runs.

## Architecture

```
Automation.UI (Razor Pages web app)
├── references Automation.Link (orchestration, validation, diagnostics)
├── references Automation (FHIR generation, helpers, config)
├── references LinkSdk (service API clients)
├── references Shared (domain models, Kafka topics, DB entities)
├── Controllers/         — MVC controllers
├── Views/               — Razor views
├── Models/              — view models and enums
├── Services/            — run management, snapshot polling, persistence
└── Services/Persistence — MongoDB-backed snapshot and run storage
```

## Features

### Run management

- **Start runs** — configure scenario kind, measure, patient count, resources per patient, seed, and patient eligibility profiles from the UI.
- **Monitor runs** — real-time log streaming via SignalR (`RunHub`), pipeline snapshot polling, and status updates.
- **View run details** — detailed run view with tabs for pipeline snapshot, data acquisition logs, and raw logs.
- **Delete runs** — remove completed runs and their stored snapshots.

### Scenario kinds

| Scenario | Description |
|---|---|
| `SmokeTest` | Single-patient quick verification |
| `MultiPatientTest` | Multi-patient volume scenario |
| `MegaPatientTest` | High-volume stress scenario |
| `Custom` | User-defined patient count and configuration |

### Measure support

Runs can target one or more measures:

- NHSN Acute Care Hospital Monthly Initial Population
- NHSN Acute Care Hospital Daily Initial Population
- NHSN Glycemic Control Hypoglycemic Initial Population

### Pipeline snapshot

The UI polls pipeline state via `StoreBackedServicePoller` and persists snapshots to MongoDB. Snapshots include:

- Report schedule and entry state
- Data acquisition log summaries (with reference log indicators)
- Normalization operation results
- MeasureEval population results
- Validation results

### Data acquisition log drill-down

The run detail page includes a modal for browsing DA logs with:

- Paginated, sortable log table
- Per-log detail view with FHIR query parameters and resource types
- Reference log identification

### API stability execution

- Includes an API stability run mode that validates broad service endpoint reachability and request/response compatibility via `LinkSdk` clients.

## Controllers

| Controller | Purpose |
|---|---|
| `HomeController` | Landing page |
| `RunsController` | Run CRUD, pipeline snapshot, DA log proxy endpoints |
| `AccountController` | Authentication (login/logout/access-denied) |

## Services

| Service | Purpose |
|---|---|
| `AutomationRunManager` | Orchestrates full run lifecycle — generation, loading, facility setup, report generation, polling, validation, cleanup |
| `RunSnapshotOrchestrator` | Manages `StoreBackedServicePoller` instances per active run |
| `StoreBackedServicePoller` | Background polling loop that persists pipeline state to MongoDB |
| `RunAutomationOutput` | `IAutomationOutput` implementation that captures logs and pushes to SignalR |
| `RunHub` | SignalR hub for real-time log streaming to connected clients |

`ConsoleAutomationOutput` is used as the base console writer where direct console output is needed.

## Persistence (MongoDB)

Run state and pipeline snapshots are persisted to MongoDB via `ISnapshotStore` / `MongoSnapshotStore`:

- `automation_runs` — run metadata, status, logs
- `pipeline_snapshots` — per-run pipeline state snapshots

Data persists across process restarts. Multiple UI instances can read the same data.

## Authentication

Supports two modes:

- **OpenID Connect** — full OIDC flow with cookie-based sessions (production).
- **Anonymous** — `Authentication:EnableAnonymousAccess=true` bypasses all auth (development).

When anonymous mode is enabled, outbound `LinkSdk` calls are configured with `AllowAnonymous=true` and skip bearer token attachment.

## Configuration

Primary configuration is via `appsettings.json` / `appsettings.Docker.json` / environment variables:

| Section | Purpose |
|---|---|
| `Automation` | `AutomationConfig` — API URLs, FHIR URLs, database connections, Kafka, auth |
| `ServiceInformation` | Service name and version |
| `MongoDB` | Connection string and database name for run/snapshot persistence |
| `Authentication` | OIDC settings or anonymous bypass |
| `LinkTokenService` | Signing key for service-to-service bearer tokens |

## Running

### Local development

```bash
dotnet run --project DotNet/Automation.UI/Automation.UI.csproj
```

### Docker

```bash
docker compose up automation-ui
```

The Dockerfile is a multi-stage build that copies all required project references from the solution root.

## Notes

- Targets `.NET 8`.
- Razor Pages / MVC hybrid (uses controllers with Razor views, not Razor Pages page model).
- SignalR is used for real-time log push — the client subscribes to a run group via `RunHub.SubscribeRun(runId)`.
- Snapshot/run state is persisted in MongoDB (`automation_runs`, `pipeline_snapshots`) and can be shared across multiple UI instances.
