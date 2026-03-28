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

- `SmokeTest` - single-patient, fast feedback path.
- `MegaPatientTest` - high-volume scenario.
- `MultiPatientTest` - multi-patient volume scenario.

## Project behavior highlights

- **Deterministic generation** using explicit seeds.
- **Background diagnostics monitoring** with event-driven output.
- **Deep ABS validation** via `ReportAbsManifestValidator`.
- **Baseline comparison** for static test scenarios via `ValidationBaselineManager`.
- **FHIR snapshot output** saved locally only when generated bundle content changes.

## Running

From repo root:

```bash
dotnet test Tests/BackendE2ETests/BackendE2ETests.csproj
```

## Configuration (environment variables)

Primary environment variables used by `TestConfig` include:

| Variable | Purpose | Default |
|---|---|---|
| `EXTERNAL_FHIR_SERVER_BASE_URL` | FHIR base URL reachable from test host | `http://localhost:6157/fhir` |
| `INTERNAL_FHIR_SERVER_BASE_URL` | FHIR base URL reachable from Link services | `http://fhir-server:8080/fhir` |
| `ADMIN_BFF_BASE_URL` | Admin BFF API base URL | `http://localhost:8063/api` |
| `LOKI_BASE_URL` | Loki base URL for diagnostics scraping | `http://localhost:3100` |
| `CLEANUP_SMOKE_TEST_DATA` | Whether to expunge FHIR data during cleanup | `false` |
| `*_REMOVE_FACILITY_CONFIG` | Whether tests remove facility config during cleanup | `false` |

Also see scenario-specific values in `TestConfig` for `ADHOC_REPORTING_SMOKE_TEST`, `MEGA_PATIENT_TEST`, and `MULTI_PATIENT_TEST` prefixes.

## Generated FHIR snapshots

Generated bundles are written under:

- `E2E_GENERATED_FHIR_OUTPUT_PATH` (if set), otherwise
- `generated-fhir-snapshots/<TestName>` beneath test runtime output base.

Writes are hash-gated (skip write when content is unchanged).

## Baseline validation

`ValidationBaselineManager` stores and compares baseline documents for static test scenarios.

Default baseline location:

- `Tests/BackendE2ETests/Baselines/<TestName>.baseline.json`

Optional override:

- `E2E_BASELINE_DIR`

Regeneration switch:

- `E2E_BASELINE_REGENERATE=true` to regenerate baseline files.

If no baseline exists, tests create one automatically.

## Diagnostics output

Tests consume diagnostics events and render concise output lines.
Noisy cadence events are suppressed, while important pipeline, issue, and stop/failure signals are preserved.

## Cleanup behavior

Cleanup is configurable and intentionally conservative by default:

- facility deletion disabled by default
- FHIR expunge disabled by default

Enable cleanup explicitly through environment variables when desired.