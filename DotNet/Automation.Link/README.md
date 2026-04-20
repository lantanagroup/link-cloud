# Automation.Link

`Automation.Link` is the Link-specific orchestration and validation layer that turns the
platform-agnostic `Automation` engine into a full end-to-end Link pipeline runner.

It bridges generated test input and expected outcomes with concrete Link services (Report,
DataAcquisition, Normalization, Tenant, Validation, Submission, Census) through `LinkSdk`
clients.

This README is aimed at three audiences:

- **Product owners / project managers** � sections 1 and 2 describe the project's
  responsibilities and where it sits in the larger system.
- **QA** � sections 3 and 5 describe the monitoring and validation surfaces, and what failure
  messages mean.
- **Developers** � sections 4 through 9 walk through each component and extension point.

---

## 1. What this project is

At a business/process level, `Automation.Link` does four jobs:

1. **Environment setup** for a test run (facility, query plans/config, normalization ops).
2. **Pipeline execution orchestration** (trigger report generation, poll for completion).
3. **Real-time diagnostics and progress tracking** (Loki/Kafka/database signals).
4. **Post-run validation and cleanup**.

If `Automation` is the simulator, `Automation.Link` is the conductor and inspector for Link.

---

## 2. Architecture

```
Automation.Link
+-- references Automation   (generation, manifest, prediction, helpers, config base classes)
+-- references LinkSdk      (service clients used for orchestration)
+-- references Shared       (domain models, enums, integration contracts)
```

Layer responsibilities within the project:

```
Automation.Link
+-- Services/          orchestration helpers (report, validation API)
+-- Helpers/           data reader, snapshots, progress tracking, setup, query plan builder
+-- Monitoring/        background monitor, probes (Loki, Kafka, progress, milestone)
+-- Validation/        strict prediction-vs-actual validators per persistence layer
+-- Configuration/     Link-specific runtime and scenario configs
+-- Models/            run summary, status, snapshot store abstraction
```

---

## 3. End-to-end workflow

A typical host-driven flow (`BackendE2ETests` or `Automation.UI`) composed through
`Automation.Link`:

1. **Load measures** � `MeasureLoader.LoadAllAsync(�)` loads measure bundles into MeasureEval
   and Validation.
2. **Ensure tenant/facility and acquisition config**
   - `FacilitySetupHelper.EnsureFacilityAsync(�)`
   - `EnsureNormalizationConfigAsync(�)`
   - `EnsureQueryPlansAsync(�)`
   - `EnsureQueryConfigAsync(�)`
   - `EnsureQueryDispatchConfigAsync(�)`
3. **Generate and submit report** � `ReportApiHelper.GenerateReportAsync(�)` (or the
   regeneration flow); then `ReportApiHelper.CheckSubmissionStatusAsync(�)`.
4. **Monitor pipeline in background** � `BackgroundDiagnosticsMonitor` + probes continuously
   inspect Loki/Kafka/progress/milestones.
5. **Read snapshots and validate state** � `PipelineSnapshot` /
   `PipelineSummarySnapshotBuilder`, then validators under `Validation/`.
6. **Cleanup** � `RunCleanupHelper.CleanupAfterRunAsync(�)`.

---

## 4. Core components

### 4.1 Setup and execution layer (`Services/`, `Helpers/`)

- **`ReportApiHelper`** � start report generation/regeneration, compute adaptive polling
  timeouts, download submission ZIP artifacts.
- **`FacilitySetupHelper`** � idempotent setup/teardown of tenant + facility +
  acquisition + normalization + query-dispatch configuration.
- **`MeasureLoader`** � load measure resources from `file://`, `resource://`, or
  `http(s)://` sources. Splits resources by consumer (MeasureEval vs Validation) and tracks
  `MeasureIds` for multi-measure runs. **Preserves `Measure.supplementalData` as authored** so
  MeasureEval executes measure-authored SDE logic.
- **`QueryPlanBuilder`** � converts `QueryPlanInput` into DataAcquisition wire format;
  delegates canonical defaults and acquired-type extraction to `Automation.QueryPlanDefaults`.
- **`AdminBffClientFactory`** � builds authenticated Admin BFF clients for flows that require
  BFF endpoints.
- **`ValidationApiHelper`** � initializes validation artifacts and categories with retry +
  Loki diagnostics hooks.

### 4.2 Data-read abstraction layer

- **`PipelineDataReader`** � central read-only facade over service APIs. Returns strongly typed
  records for schedules, entries, populations, DA summaries, normalization ops, facility
  state, etc. Includes an 8-second TTL cache that collapses duplicate high-frequency queries
  across diagnostics, validators, and UI pollers.
- **`DatabaseConnectionFactory`** � direct DB connection string helper for validators that
  need SQL access (notably Validation DB checks).

### 4.3 Monitoring and diagnostics layer (`Monitoring/`)

- **`BackgroundDiagnosticsMonitor`** � hosts a probe-driven monitor loop, emits
  `AutomationMonitorEvent`, supports streaming events and milestone waits, and exposes
  `HasCriticalFailure` so hosts can short-circuit polling.
- **Probes**
  - `LokiErrorProbe` � scans service logs for exceptions and errors.
  - `KafkaErrorProbe` � flags retry/DLQ topics as critical failures.
  - `ProgressProbe` � runs `ProgressMonitor` and stall detection.
  - `MilestoneProbe` � runs `MilestoneValidationOrchestrator`.
- **`ProgressMonitor` + `PipelineProgressTracker`** � compute coarse progress across
  DataAcquisition � MeasureEval � Validation � Submission, identify the stalled stage,
  compute stall duration, and write human-readable diagnostics with per-status breakdowns.
- **`LokiScraper`, `KafkaErrorMonitor`** � infrastructure-specific signal collectors used by
  probes and retry diagnostics.

### 4.4 Pipeline snapshots (`Helpers/`)

- **`PipelineSnapshot`** � full non-asserting diagnostic snapshot output.
- **`PipelineSummarySnapshotBuilder`** � compact snapshot payloads used by UI polling surfaces
  (schedule, entries, populations, acquisition, measure resources, validation resources).
  Consumed by `Automation.UI.StoreBackedServicePoller`.

---

## 5. Validation suite (`Validation/`)

Validators consume `PipelineDataReader` and downloaded artifacts, fail the run on invariant
violations, and share a common goal: prove that persisted state and output artifacts reflect
expected pipeline behavior.

### 5.1 Strict prediction-vs-actual reconciliation

When a `GenerationManifest` is available, the count-based validators compare predicted vs
actual counts with **strict equality**: `actual == expected` for every resource type, both
directions. Messages clearly distinguish the two failure shapes:

| Shape | Meaning |
|---|---|
| `expected=N, actual<N` or `missing expected resource <key>` | Pipeline dropped/filtered something we predicted would land. |
| `expected=N, actual=M (ABS has M-N more than predicted)` | Pipeline produced something the prediction did not anticipate. |

There is no tolerant `actual >= expected` mode. Pipeline-derived types (`Patient`,
`MeasureReport`, `OperationOutcome`) are predicted deterministically from the manifest so the
strict mode holds.

### 5.2 Validators

- **`ReportAbsManifestValidator`** � validates internal ABS artifacts (`manifest.ndjson`,
  `patient-*.ndjson`). Reconciles expected vs actual resources using
  `GenerationManifest.GetExpectedAbsCountsForPatient(patientId)`.

  Before running, it reads `ReportEntry.ReportingStatus` rows via `PipelineDataReader` and
  populates `manifest.ExpectedOperationOutcomeCountByPatient[pid] = 1` for every patient whose
  status is `FailedValidation`. This matches the Report service's behavior:
  `ValidationCompleteListener.ProcessMessageAsync` appends exactly one OperationOutcome to the
  patient ABS blob when `ValidationComplete.IsValid == false`.

- **`ReportDatabaseValidator`** � validates schedule, report entries, report types,
  populations, and report resource persistence. Uses
  `GenerationManifest.GetExpectedReportResourceCountsForPatient(patientId)`, which omits
  OperationOutcome because those resources are appended directly to the ABS blob and never
  reach the `ReportResource` table.

- **`DataAcquisitionDatabaseValidator`** � validates query config/plans, acquisition logs,
  FHIR query rows, and reference resources.

- **`NormalizationDatabaseValidator`** � validates operations, operation resource types, and
  operation sequencing.

- **`TenantDatabaseValidator`** � validates facility persistence and scheduled report
  configuration.

- **`ValidationResultsValidator`** � validates Validation service API availability/results
  and exception-free logs.

- **`MilestoneValidationOrchestrator`** � lightweight idempotent stage checkpoints used
  during run-time monitoring.

### 5.3 Validator isolation pattern

When invoked from `Automation.UI`, validators are wrapped in a `RunValidator` helper that:

- Catches exceptions per validator so one failure doesn't prevent subsequent validators from
  running.
- Persists partial `validatorResults` snapshots after each validator completes.
- Ensures partial results are visible in the UI dashboard even if a later validator throws.

This is distinct from test-host invocation (`BackendE2ETests`), where validator failures are
expected to throw and fail the test.

---

## 6. Cleanup

- **`RunCleanupHelper`** � standard post-run cleanup path shared by hosts. Supports
  cancellation-specific cleanup ordering:
  1. Cancel DA work.
  2. Soft-delete run artifacts.
  3. Remove facility-level config.
  4. Expunge generated FHIR data.

Each phase respects its own toggle so that a partial cleanup (e.g., expunge FHIR but keep
service data for inspection) is expressible.

---

## 7. Configuration (`Configuration/`)

- **`AutomationConfig`** � Link-specific runtime settings (service URLs, FHIR endpoints,
  OAuth/basic auth, DB/Kafka settings, query behavior).
- **`TestScenarioConfig`** � scenario-specific runtime parameters (measure bundle locations,
  patient IDs, polling windows, cleanup flags).

Both extend base classes in `Automation.Configuration`.

---

## 8. Key models

- **`AutomationRunSummary`** � normalized run metadata/status payload consumed by hosts.
  Fields include: `RunId`, `RunName`, `Scenario`, `Status`, `PatientCount`,
  `ResourcesPerPatient`, `Seed`, `CreatedAt`, `FinishedAt`, `Duration`,
  `RunConfigurationJson`.
- **`AutomationRunStatus` / `AutomationScenarioKind`** � run lifecycle and scenario identity
  enums.
- **`ISnapshotStore`** � abstraction used by hosts (notably the UI) for run metadata, domain
  snapshots, and log persistence. `GetAllRunSummariesAsync(since)` supports date filtering to
  bound collection scans.

---

## 9. Runtime behavior characteristics

- **Idempotent setup-first design** � facility and query configuration methods are safe to
  call repeatedly.
- **Monitoring-first failure detection** � critical errors can short-circuit long polling
  loops.
- **Deterministic validation** � validators consume `GenerationManifest` to compare concrete
  expected vs actual state.
- **Host-agnostic composition** � the same project powers both automated tests and
  interactive UI flows.
- **Validator isolation under the UI** � partial results flow to the dashboard even when
  later validators throw.

---

## 10. Intended usage

Primary consumers:

- **`Tests/BackendE2ETests`** � CI/E2E orchestration host.
- **`DotNet/Automation.UI`** � interactive Razor MVC host for run management.

---

## 11. Notes

- Targets `.NET 8`.
- Designed for DI-based composition in host applications/tests.
- Uses global usings to re-export core `Automation` types (`IAutomationOutput`,
  `ConsoleAutomationOutput`, `RetryHelper`, etc.), so host code does not need to import both
  namespaces.
- Strict prediction-vs-actual reconciliation is the default. The `Automation` README documents
  the prediction formula that drives these validators.
