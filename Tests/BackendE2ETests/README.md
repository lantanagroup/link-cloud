# BackendE2ETests

End-to-end integration tests that exercise the full Link reporting pipeline against a running
Link environment (FHIR server, DataAcquisition, Normalization, MeasureEval, Validation, Report,
Submission, Tenant, Census). Each test generates synthetic FHIR data with a deterministic seed,
drives the pipeline from start to finish, and validates every persistence layer and artifact
against a precise, input-derived expectation model.

This README is intended to answer three questions:

- **What does each test do?** (high-level, for product owners and project managers)
- **How do I run them and what do I need to configure?** (for QA and engineers)
- **How do they validate correctness?** (for engineers debugging failures)

---

## 1. Purpose and scope

BackendE2ETests validates that:

1. A configured Link environment can ingest FHIR data via DataAcquisition,
2. MeasureEval correctly evaluates measure CQL and produces `MeasureReport` artifacts,
3. The Validation service produces expected results (and `OperationOutcome` resources for failures),
4. The Report service aggregates everything into per-patient NDJSON files and a manifest,
5. Submission produces the downloadable ZIP artifact,
6. Every persistence layer (Tenant, DataAcquisition, Normalization, Report, Validation DBs)
   contains exactly the records expected given the generated input.

The suite uses **strict prediction-vs-actual reconciliation**: generated inputs drive a
deterministic prediction of what every downstream layer should contain, and validators fail the
run if actual state differs from prediction in either direction. This replaces brittle baseline
snapshots with input-derived expectations.

---

## 2. Test suites

Each suite is a `[Fact]` class exercising a specific workflow. Every data-generating test shares
its seed, patient count, resource range, and patient prefix with a corresponding system scenario
in `Automation.UI`'s `ScenarioSeedService`, so the backend run and the UI run produce bit-for-bit
identical FHIR input.

| Suite | What it exercises | Seed | Patients | Resources |
|---|---|---:|---:|---:|
| `AdhocReportTest` | Single-patient ad-hoc reporting happy path. | 20260326 | 1 | 1000 |
| `MultiPatientTest` | Volume scenario across many patients. | 20260328 | 150 | 25-50 |
| `MegaPatientTest` | Single-patient stress test (thousands of resources). | 20260327 | 1 | 5000 |
| `MegaMultiPatientTest` | Hybrid: one mega patient + 149 normal patients. | 20260330 | 150 | 5000 / 25-50 |
| `ReportScheduledWorkflowTest` | Scheduled (non-ad-hoc) report generation workflow. | 20260326 | 1 | 1000 |
| `RegenerateReportTest` | Report regeneration after initial submission. | 20260401 | 1 | 100 |
| `MultiMeasureTest` | ACH Monthly + Glycemic Control (Hypoglycemic) simultaneously; one patient qualifies for both, one for ACH only. | 20260420 | 2 | 250 |
| `ApiStabilityTest` | Exercises service API surfaces (Tenant, Census, DataAcquisition, Normalization, QueryDispatch) without running a full report pipeline. | Yes | Yes | Yes |

### Why backend and UI scenarios share configuration

Both hosts call into the same `Automation` generator with the same inputs. If the UI passes, the
backend test should pass with the same data and the same expectations -- and vice versa. When a
discrepancy is investigated, any developer can reproduce either test run from either host and
get the exact same FHIR bundles. The `Automation.UI` **Scenarios** page is therefore a
user-friendly mirror of this test suite.

---

## 3. How each test works

All data-generating tests follow the same flow:

1. **Generate FHIR data** via `FhirGenerationPipeline.GenerateAndUploadAsync(...)`
   - Uses the test's seed, patient cohorts, and selected measures.
   - Streams bundles to FHIR in dependency-safe order (Patient -> Encounter -> Observations).
   - Builds a `GenerationManifest` incrementally, recording every `Type/Id` key generated.
   - Runs `QueryPlanAcquisitionSimulator` against each patient to record simulated-acquired keys.
   - Runs `CqlFilterSimulator` to record keys that will be filtered out by measure SDE semantics.

2. **Load measures** into MeasureEval and Validation via `MeasureLoader.LoadAllAsync(...)`.

3. **Ensure facility/config** via `FacilitySetupHelper` (idempotent -- safe to run repeatedly):
   tenant + facility + normalization ops + query plans + query config + query dispatch config.

4. **Start report generation** via `ReportApiHelper.GenerateReportAsync(...)` (or the
   regeneration flow for `RegenerateReportTest`).

5. **Monitor until completion** via `BackgroundDiagnosticsMonitor` with Loki/Kafka/progress/
   milestone probes. Critical failures (DLQ, service exceptions) short-circuit long polls.

6. **Download ABS artifacts** (external submission zip + internal manifest+NDJSON files) via
   `ReportApiHelper.DownloadAbsArtifactsAsync(...)`.

7. **Run the validator suite** against every persistence layer and artifact:
   - `ReportAbsManifestValidator` (strict prediction vs actual for manifest + patient NDJSONs)
   - `ReportDatabaseValidator` (ReportEntry, ReportResource, ReportPopulation rows)
   - `DataAcquisitionDatabaseValidator` (query config/plans, acquisition logs, FHIR query rows)
   - `NormalizationDatabaseValidator` (operations, sequencing)
   - `TenantDatabaseValidator` (facility + scheduled report state)
   - `ValidationResultsValidator` (API availability + exception-free logs)

8. **Cleanup** via `RunCleanupHelper` (configurable).

`ApiStabilityTest` skips steps 1, 4, 5, 7 and instead walks the service APIs directly to verify
each endpoint is reachable and well-formed.

### Strict prediction-vs-actual reconciliation

The manifest and database validators do not accept `actual >= expected`; they require
`actual == expected` for every resource type on every patient, both directions.

The predicted counts come from `GenerationManifest.GetExpectedAbsCountsForPatient(patientId)`
(for ABS NDJSON) or `GetExpectedReportResourceCountsForPatient(patientId)` (for the
`ReportResource` DB). Both start from the same generated-resource set, intersect it with the
query plan's acquired types and the CQL-referenced types, subtract per-resource SDE exclusions,
and then add deterministic pipeline-derived counts:

| Type | Prediction rule |
|---|---|
| `Patient` | 1 per patient qualifying for any selected measure (MeasureEval loads Patient implicitly). |
| `MeasureReport` | One per patient per qualifying measure (MeasureEval writes exactly one per pair). |
| `OperationOutcome` | 1 for every patient whose `ReportEntry.ReportingStatus == FailedValidation`. ABS-only -- bypasses the `ReportResource` table by design. |

When multiple measures are selected and a patient only qualifies for some of them,
`CqlFilterSimulator` intersects only the rule-sets of the patient's *qualifying* measures
(non-qualifying measures do not contribute contained resources to ABS, so their SDE rules do
not apply to that patient).

---

## 4. Running the tests

From the repository root:

```bash
# Run everything
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj

# Run a single suite
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter FullyQualifiedName~AdhocReportTest

# Run with detailed output (useful for debugging failures)
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --logger "console;verbosity=detailed"
```

### Prerequisites

A complete Link environment must be running and reachable from the test host. The tests expect:

- FHIR server (default: `http://localhost:6157/fhir`)
- Admin BFF (default: `http://localhost:8063/api`)
- Loki for log diagnostics (default: `http://localhost:3100`)
- All Link services, Kafka, MongoDB, SQL databases, Keycloak

The `docker compose` file at the repository root provisions a working stack suitable for local
runs.

---

## 5. Configuration

Tests read configuration from environment variables via `TestConfig`. The most commonly used
variables are below; suite-specific variables use the prefix shown in each test's
`BuildScenarioConfig(...)` call (e.g. `ADHOC_REPORT_TEST`, `MEGA_PATIENT_TEST`,
`MULTI_MEASURE_TEST`).

| Variable | Purpose | Default |
|---|---|---|
| `FHIR_SERVER_BASE_URL` | FHIR base URL the test process itself reaches. (Legacy alias: `EXTERNAL_FHIR_SERVER_BASE_URL`.) | `http://localhost:6157/fhir` |
| `FACILITY_FHIR_SERVER_BASE_URL` | FHIR base URL registered on each test facility's query config; Link's services in docker read this back. (Legacy alias: `INTERNAL_FHIR_SERVER_BASE_URL`.) | `http://fhir-server:8080/fhir` |
| `ADMIN_BFF_BASE_URL` | Admin BFF base URL | `http://localhost:8063/api` |
| `LOKI_BASE_URL` | Loki base URL for log scraping | `http://localhost:3100` |
| `E2E_GENERATED_FHIR_OUTPUT_PATH` | Override for where generated FHIR bundle snapshots land | `<test-dir>/generated-fhir-snapshots/<TestName>` |
| `<PREFIX>_CLEANUP_FHIR_DATA` | `true`/`false` -- expunge generated FHIR resources after the run | `true` |
| `<PREFIX>_CLEANUP_SERVICE_DATA` | `true`/`false` -- remove facility/report/DA/query-dispatch rows after the run | `false` |

Cleanup is conservative by default: FHIR data is expunged to keep the server lean, but service
data is preserved so investigators can inspect the run state after a failure.

---

## 6. Generated FHIR snapshot output

Every data-generating test writes its bundles to disk for post-run inspection. The output
directory is either `E2E_GENERATED_FHIR_OUTPUT_PATH` or
`generated-fhir-snapshots/<TestName>` below the test runtime output directory.

Writes are hash-gated -- if the current run's content hash matches the previous run's, the files
are skipped. This keeps the snapshots useful as diffable artifacts (any content drift shows up
immediately) without flooding source control with identical outputs.

---

## 7. Diagnostics during a run

Each test captures real-time diagnostics through `BackgroundDiagnosticsMonitor`:

- **LokiErrorProbe** -- scans service logs for exceptions/errors.
- **KafkaErrorProbe** -- flags retry/DLQ topics.
- **ProgressProbe** -- computes pipeline progress (DA -> MeasureEval -> Validation -> Submission)
  and detects stalls.
- **MilestoneProbe** -- records idempotent progress checkpoints.

Human-readable lines are written to test output. Cadence-driven "heartbeat" events are
suppressed; only meaningful pipeline transitions, issues, and stop/failure signals surface.

If a critical failure is detected (e.g., service DLQ, unhandled exception cluster) the test can
abort long polls immediately instead of waiting for the submission timeout.

---

## 8. What "strict" means when a test fails

A failing run produces a targeted message. Examples:

```
ABS patient=MultiMeasurePatient-002-0dffadf3, type=Condition:
    expected=21 (sim-acquired n reachable-CQL + derived), actual=17.
ABS artifacts missing expected resource: Condition/MultiMeasurePatient-002-0dffadf3-Condition-075
```

Three common failure modes and what they mean:

| Message shape | Root cause pattern |
|---|---|
| `expected=N, actual=M (ABS has M-N more than predicted)` | Something landed in ABS that the prediction model didn't expect. The generator, query plan, or CQL filter simulator is drifting from reality. |
| `expected=N, actual<N` or `missing expected resource` | Something we expected was filtered, dropped, or never written. Usually a CQL retrieve missing, a normalization op deleting a resource, or DA skipping a query. |
| `ReportResource count-type=OperationOutcome` deviation | OperationOutcome is ABS-only; ReportResource prediction excludes it by design. If DB has an OO, something is writing through `ReportResourceManager` unexpectedly. |

These messages are designed to point an investigator at the exact boundary that drifted.

---

## 9. Architecture

```
BackendE2ETests
+-- references Automation.Link  (orchestration, validation, monitoring, config)
+-- references Automation       (FHIR generation, manifest, prediction model)
+-- references LinkSdk          (service API clients)
+-- references Shared           (domain models, enums, integration contracts)
```

No direct Link service dependencies -- everything flows through `Automation.Link` helpers and
`LinkSdk` clients.

---

## 10. Notes

- Targets `.NET 8`.
- All tests are `[Fact]` classes using `IClassFixture<BackendE2ETestFixture>` for shared setup.
- `FhirGenerationPipeline` is preferred over `FhirBundleGenerator.Generate()` for memory-bounded
  generation (see `DotNet/Automation/README.md`).
- Facility IDs are generated with a per-run `Guid` suffix (`AdhocReportTest-<guid>`) so parallel
  runs do not collide.
- The strict prediction model is defined in `GenerationManifest` (see `Automation/README.md`)
  and consumed by validators in `Automation.Link/Validation/` (see `Automation.Link/README.md`).
- Each backend test has a matching system scenario in `Automation.UI` with identical seed,
  patient count, resource range, and prefix -- so UI and backend runs are reproducible from
  either host.
