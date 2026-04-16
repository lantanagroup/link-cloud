# Automation.Link

`Automation.Link` is the Link-specific orchestration and validation layer that turns the platform-agnostic `Automation` engine into a full end-to-end Link pipeline runner.

It bridges generated test input and expected outcomes with concrete Link services (Report, DataAcquisition, Normalization, Tenant, Validation, Submission) through `LinkSdk` clients.

---

## What this project is

At a business/process level, `Automation.Link` does four jobs:

1. **Environment setup** for a test run (facility, query plans/config, normalization ops)
2. **Pipeline execution orchestration** (trigger report generation, poll for completion)
3. **Real-time diagnostics and progress tracking** (Loki/Kafka/database signals)
4. **Post-run validation and cleanup**

Think of `Automation` as the simulator and `Automation.Link` as the conductor + inspector for Link.

---

## Architecture

```
Automation.Link
├── references Automation (generation, manifests, base helpers, config base classes)
├── references LinkSdk (service clients used for orchestration)
└── references Shared (domain models, enums, integration contracts)
```

---

## End-to-end workflow

Typical host-driven flow (`BackendE2ETests` or `Automation.UI`) composed through `Automation.Link`:

1. **Load measures**
   - `MeasureLoader.LoadAllAsync()` loads measure bundles into MeasureEval and Validation services
2. **Ensure tenant/facility and acquisition config**
   - `FacilitySetupHelper.EnsureFacilityAsync(...)`
   - `EnsureNormalizationConfigAsync(...)`
   - `EnsureQueryPlansAsync(...)`
   - `EnsureQueryConfigAsync(...)`
   - `EnsureQueryDispatchConfigAsync(...)`
3. **Generate and submit report**
   - `ReportApiHelper.GenerateReportAsync(...)` or regeneration flow
   - `ReportApiHelper.CheckSubmissionStatusAsync(...)`
4. **Monitor pipeline in background**
   - `BackgroundDiagnosticsMonitor` + probes continuously inspect Loki/Kafka/progress/milestones
5. **Read snapshots and validate state**
   - `PipelineSnapshot` / `PipelineSummarySnapshotBuilder`
   - validators under `Validation/`
6. **Cleanup**
   - `RunCleanupHelper.CleanupAfterRunAsync(...)`

---

## Core process/object relationships

### 1) Setup and execution layer

- `ReportApiHelper`
  - starts report generation/regeneration
  - polls schedule status until submitted
  - downloads external and internal ABS artifacts
- `FacilitySetupHelper`
  - creates or ensures tenant + facility configuration
  - provisions query plans, FHIR query config, normalization ops, query dispatch settings
- `MeasureLoader`
  - loads measure definition resources into MeasureEval
  - loads validation artifacts into Validation service
  - sanitizes orphaned supplementalData CQL expressions to prevent runtime failures

### 2) Data-read abstraction layer

- `PipelineDataReader`
  - central read-only facade over service APIs
  - provides strongly typed records for schedules, entries, populations, DA summaries, normalization ops, facility state
  - includes an 8-second TTL cache to collapse duplicate high-frequency queries across diagnostics/validators/UI pollers
- `DatabaseConnectionFactory`
  - direct DB connection string helper for validators that need SQL access (notably Validation DB checks)

### 3) Monitoring and diagnostics layer

- `BackgroundDiagnosticsMonitor`
  - hosts a probe-driven monitor loop and emits `AutomationMonitorEvent`
  - supports streaming events and milestone waits
  - exposes `HasCriticalFailure` for early abort decisions
- `MonitorProbes`
  - `LokiErrorProbe`: scans service logs
  - `KafkaErrorProbe`: flags retry/DLQ-like Kafka failures as critical
  - `ProgressProbe`: runs `ProgressMonitor` and stall detection
  - `MilestoneProbe`: runs `MilestoneValidationOrchestrator`
- `ProgressMonitor` + `PipelineProgressTracker`
  - compute coarse progress across DA → MeasureEval → Validation → Submission
  - identify stalled stage and stall duration
  - write human-readable diagnostics with status breakdowns
- `LokiScraper` / `KafkaErrorMonitor`
  - infrastructure-specific signal collectors used by probes and retry diagnostics

### 4) Validation layer

Validators consume `PipelineDataReader` + artifacts and fail the run on invariant violations:

- `ReportDatabaseValidator`
  - validates schedule, report entries, report types, populations, and report resource persistence
  - supports `GenerationManifest`-aware expectations for multi-measure/profiled runs
- `ReportAbsManifestValidator`
  - validates internal ABS artifacts (`manifest.ndjson`, `patient-*.ndjson`)
  - reconciles expected generated/acquired/CQL-reachable resources vs actual ABS output
- `DataAcquisitionDatabaseValidator`
  - validates query config/plans, acquisition logs, FHIR query rows, and reference resources
- `NormalizationDatabaseValidator`
  - validates operations, operation resource types, and operation sequencing
- `TenantDatabaseValidator`
  - validates facility persistence and scheduled report configuration
- `ValidationResultsValidator`
  - validates Validation service API availability/results and exception-free logs
- `MilestoneValidationOrchestrator`
  - lightweight idempotent stage checkpoints used during run-time monitoring

### Validator isolation pattern

When invoked from `Automation.UI`, validators are wrapped in a `RunValidator` helper that:
- Catches exceptions per-validator so one failure doesn't prevent subsequent validators from running
- Persists partial `validatorResults` snapshots after each validator completes
- Enables partial results to be visible in the UI dashboard even if a later validator throws

This is distinct from test-host invocation (`BackendE2ETests`) where validator failures are expected to throw and fail the test.

### 5) Cleanup layer

- `RunCleanupHelper`
  - standard post-run cleanup path shared by hosts
  - supports cancellation-specific cleanup ordering:
    1. cancel DA work
    2. soft-delete run artifacts
    3. remove facility-level config
    4. expunge generated FHIR data

---

## Detailed component summaries

## Service orchestration (`Services/`)

- `ReportApiHelper`
  - report generation/regeneration
  - adaptive polling timeout calculation
  - submission ZIP artifact download and expansion
- `ValidationApiHelper`
  - initializes validation artifacts and categories with retry + Loki diagnostics hooks

## Pipeline data access and snapshots (`Helpers/`)

- `PipelineDataReader`
  - shared read model for report/DA/normalization/tenant state
- `PipelineSnapshot`
  - full non-asserting diagnostic snapshot output
- `PipelineSummarySnapshotBuilder`
  - compact snapshot payloads used by UI polling surfaces
  - builds domain-specific summaries (schedule, entries, populations, acquisition, measure resources, validation resources) for the `StoreBackedServicePoller`
- `PipelineProgressTracker`
  - progress percentage + stall-stage computation
  - tracks pipeline stages: DataAcquisition → MeasureEval → Validation → Submission
  - computes per-stage completion percentages based on expected vs actual counts

## Setup and query plan utilities (`Helpers/`)

- `FacilitySetupHelper`
  - idempotent setup/teardown of tenant/acquisition/normalization/query-dispatch configuration
- `QueryPlanBuilder`
  - converts `QueryPlanInput` into DataAcquisition API wire format (`JObject`)
  - delegates canonical default/resource-type extraction to `Automation` (`QueryPlanDefaults`)
- `AdminBffClientFactory`
  - builds authenticated Admin BFF clients for flows that require BFF endpoints

## Measure loading

- `MeasureLoader`
  - supports `file://`, `resource://`, and `http(s)://` measure bundle sources
  - splits bundle resources by consumer (MeasureEval vs Validation)
  - tracks `MeasureIds` for multi-measure runs

## Validation suite (`Validation/`)

- `ReportAbsManifestValidator`
- `ReportDatabaseValidator`
- `DataAcquisitionDatabaseValidator`
- `NormalizationDatabaseValidator`
- `TenantDatabaseValidator`
- `ValidationResultsValidator`
- `MilestoneValidationOrchestrator`

Each validator is focused on one service boundary but shares a common goal: prove that persisted state and output artifacts reflect expected pipeline behavior.

## Configuration (`Configuration/`)

- `AutomationConfig`
  - Link-specific runtime settings (service URLs, FHIR endpoints, OAuth/basic auth, DB/Kafka settings, query behavior)
- `TestScenarioConfig`
  - scenario-specific runtime parameters (measure bundle locations, patient IDs, polling windows, cleanup flags)

---

## Key models

- `AutomationRunSummary`
  - normalized run metadata/status payload consumed by hosts
  - includes: RunId, RunName, Scenario, Status, PatientCount, ResourcesPerPatient, Seed, CreatedAt, FinishedAt, Duration, RunConfigurationJson
- `AutomationRunStatus` / `AutomationScenarioKind`
  - run lifecycle and scenario identity enums
- `ISnapshotStore` (from `Models/`)
  - abstraction used by hosts (notably UI) for run metadata, domain snapshots, and log persistence
  - `GetAllRunSummariesAsync(since)` supports date filtering to bound collection scans

---

## Runtime behavior characteristics

- **Idempotent setup-first design**: facility/query configuration methods are safe to call repeatedly
- **Monitoring-first failure detection**: critical errors can short-circuit long polling loops
- **Deterministic validation support**: validators can consume `GenerationManifest` to compare concrete expected vs actual state
- **Host-agnostic composition**: the same project powers both automated tests and interactive UI flows
- **Validator isolation**: UI host wraps validators to capture partial results; test host lets failures propagate

---

## Intended usage

Primary consumers:

- `Tests/BackendE2ETests` — CI/E2E orchestration host
- `DotNet/Automation.UI` — interactive Razor-based run management host

---

## Notes

- Targets `.NET 8`.
- Designed for DI-based composition in host applications/tests.
- Uses global usings to re-export core `Automation` types (`IAutomationOutput`, `ConsoleAutomationOutput`, `RetryHelper`, etc.).
