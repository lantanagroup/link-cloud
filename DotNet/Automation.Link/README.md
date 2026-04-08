# Automation.Link

`Automation.Link` provides Link-specific orchestration, validation, and diagnostics helpers for end-to-end pipeline execution and verification.

This project bridges the platform-agnostic `Automation` library with the concrete Link service ecosystem (via `LinkSdk` clients and `Shared` models).

## Architecture

```
Automation.Link
├── references Automation (generation, base helpers, config base classes)
├── references LinkSdk (Flurl-based service API clients)
└── references Shared (domain models, Kafka topics, DB entities)
```

## What this project includes

### Service orchestration (`Services/`)

- `ReportApiHelper` — report generation, submission polling, artifact download.
- `ValidationApiHelper` — measure artifact and category initialization.

### Pipeline data access (`Helpers/`)

- `PipelineDataReader` — read-only database queries across Report, DataAcquisition, Normalization, Tenant, and Validation databases.
- `PipelineSnapshot` — non-asserting, full pipeline state dump for diagnostic output.
- `PipelineSummarySnapshotBuilder` — builds compact JSON snapshots for UI consumption.
- `PipelineProgressTracker` — tracks per-patient pipeline progress through DA → Normalization → MeasureEval → Report stages.
- `DatabaseConnectionFactory` — connection string resolution for all service databases.

### Diagnostics and monitoring (`Helpers/`)

- `BackgroundDiagnosticsMonitor` — background polling loop that emits `MonitorEvent` instances for real-time pipeline observation.
- `LokiScraper` — scrapes Loki for service log entries within a time window.
- `KafkaErrorMonitor` — monitors Kafka dead-letter/error topics.
- `MonitorProbes` — individual probe implementations (schedule, entries, populations, milestones, etc.).

### Facility and config setup (`Helpers/`)

- `FacilitySetupHelper` — creates/deletes facility, normalization, query plan, query config, and query dispatch configurations.
- `QueryPlanBuilder` — builds FHIR query plan JSON payloads for different EHR vendors.
- `AdminBffClientFactory` — creates authenticated Flurl clients for Admin BFF endpoints.

### Measure loading

- `MeasureLoader` — loads embedded measure definition bundles into MeasureEval and Validation services.

### Validation suite (`Validation/`)

- `ReportAbsManifestValidator` — deep reconciliation of ABS manifest artifacts against pipeline persistence layers (DataAcquisition, MeasureEval, Report).
- `ReportDatabaseValidator` — validates Report database state (schedules, entries, patient data).
- `DataAcquisitionDatabaseValidator` — validates DA log completeness and reference resource acquisition.
- `NormalizationDatabaseValidator` — validates normalization operation execution.
- `TenantDatabaseValidator` — validates tenant/facility configuration persistence.
- `ValidationResultsValidator` — validates Java Validation service results.
- `MilestoneValidationOrchestrator` — coordinates multi-phase milestone validation across pipeline stages.

### Configuration (`Configuration/`)

- `AutomationConfig` — extends `AutomationConfigBase` with Link-specific settings (API URLs, OAuth, database connections, Kafka, query behavior).
- `TestScenarioConfig` — extends `TestScenarioConfigBase` with concrete scenario settings (measure bundle path, patient IDs, timeouts).

## Key concepts

- `IAutomationOutput` is the common output abstraction used across all components.
- `BackgroundDiagnosticsMonitor` emits runtime `MonitorEvent` instances that consumers can observe and react to.
- All validators are non-throwing by default — they report findings through output rather than assertions, unless explicitly asked to assert.

## Intended usage

This project is consumed by:

- `Tests/BackendE2ETests` — primary consumer for E2E test orchestration.
- `DotNet/Automation.UI` — Razor Pages web UI for interactive automation runs.

## Notes

- Targets `.NET 8`.
- Most classes are designed to be composed in DI-backed test/service bootstraps.
