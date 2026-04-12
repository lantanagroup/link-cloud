# BackendE2ETests

## Overview

`BackendE2ETests` validates end-to-end Link reporting flows against a running Link environment.

The tests exercise the pipeline by:

1. generating deterministic synthetic FHIR data,
2. loading data into FHIR,
3. creating tenant/query/normalization/report configuration,
4. running report generation and submission,
5. validating downloaded ABS artifacts and pipeline persistence layers.

## Test suites

| Suite | Description |
|---|---|
| `AdhocReportTest` | Single-patient ad-hoc report test. |
| `MultiPatientTest` | Multi-patient volume scenario. |
| `MegaPatientTest` | High-volume stress scenario (thousands of resources). |
| `MultiMeasureAdhocReportingTest` | Ad-hoc reporting across multiple measures simultaneously. |
| `ReportScheduledWorkflowTest` | Scheduled (non-ad-hoc) report generation workflow. |
| `RegenerateReportTest` | Re-generation of an existing report. |
| `ApiStabilityTest` | Verifies API surface stability under normal conditions. |

## Project behavior highlights

- **Deterministic generation** using explicit seeds — same inputs always produce the same FHIR bundles.
- **Scenario-driven FHIR data** — patients are generated with clinically coherent resources driven by 16 clinical scenarios (pneumonia, MI, DKA, GI bleed, etc.) via `ScenarioResourceMap`.
- **Measure-aware profiles** — `GenerateWithProfiles()` creates qualifying and non-qualifying patients for specific measures (ACH Monthly, ACH Daily, Hypo).
- **Background diagnostics monitoring** with event-driven output.
- **Deep ABS validation** via `ReportAbsManifestValidator`.
- **Predictive validation** via deterministic generation manifests and cross-layer validators (ABS, Report DB, DA DB).
- **FHIR snapshot output** saved locally only when generated bundle content changes.

## Architecture

```
BackendE2ETests
├── references Automation.Link (orchestration, validation, config)
├── references Automation (FHIR generation, helpers)
├── references LinkSdk (service API clients)
└── references Shared (common models, extensions)
```

## Running

From repo root:

```bash
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj
```

Run a specific suite:

```bash
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj --filter "Category=AdhocReportTest"
```

## Configuration (environment variables)

Primary environment variables used by `TestConfig` include:

| Variable | Purpose | Default |
|---|---|---|
| `EXTERNAL_FHIR_SERVER_BASE_URL` | FHIR base URL reachable from test host | `http://localhost:6157/fhir` |
| `INTERNAL_FHIR_SERVER_BASE_URL` | FHIR base URL reachable from Link services | `http://fhir-server:8080/fhir` |
| `ADMIN_BFF_BASE_URL` | Admin BFF API base URL | `http://localhost:8063/api` |
| `LOKI_BASE_URL` | Loki base URL for diagnostics scraping | `http://localhost:3100` |
| `CLEANUP_ADHOC_REPORT_TEST_DATA` | Whether to expunge FHIR data during cleanup | `true` |
| `*_CLEANUP_SERVICE_DATA` | Whether tests remove Link service data (facility, reports, DA logs, query dispatch) after the run | `false` |

Also see scenario-specific values in `TestConfig` for `ADHOC_REPORT_TEST`, `MEGA_PATIENT_TEST`, and `MULTI_PATIENT_TEST` prefixes.

## Generated FHIR snapshots

Generated bundles are written under:

- `E2E_GENERATED_FHIR_OUTPUT_PATH` (if set), otherwise
- `generated-fhir-snapshots/<TestName>` beneath test runtime output base.

Writes are hash-gated (skip write when content is unchanged).

## Diagnostics output

Tests consume diagnostics events and render concise output lines.
Noisy cadence events are suppressed, while important pipeline, issue, and stop/failure signals are preserved.

## Cleanup behavior

Cleanup is configurable and intentionally conservative by default:

- facility deletion disabled by default
- FHIR expunge disabled by default

Enable cleanup explicitly through environment variables when desired.