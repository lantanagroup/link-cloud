# Automation

`Automation` provides reusable helpers and validators for end-to-end Link pipeline execution and verification.

## What this project includes

- **FHIR data generation and loading**
  - Deterministic synthetic data generation (`Generation/*`)
  - Bulk loading transaction bundles into FHIR
- **Service orchestration clients**
  - Facility, normalization, query config, report, and validation API clients (`Services/*`)
- **Diagnostics and monitoring**
  - Background diagnostics monitor
  - Event stream model for diagnostics consumption
  - Loki/Kafka/progress/milestone probes
- **Pipeline data access helpers**
  - Read-only DB access utilities for Report/DataAcquisition/Normalization/Tenant state
- **Validation suite**
  - Report ABS/manifest validation
  - Report/DataAcquisition/Normalization/Tenant database validators
  - Validation-results checks

## Key concepts

- `IAutomationOutput` is the common output abstraction used across generators, monitors, and validators.
- `BackgroundDiagnosticsMonitor` emits runtime events that consumers can observe.
- `ReportAbsManifestValidator` performs deep reconciliation against ABS artifacts and pipeline layers (DataAcquisition, MeasureEval, Report).

## Configuration

Automation components are configured through `AutomationConfig` (`Configuration/AutomationConfig.cs`), including:

- API base URLs
- OAuth/basic auth settings
- database connection settings
- Kafka settings
- query behavior settings

## Intended usage

This project is consumed by:

- `Tests/BackendE2ETests` (current primary consumer)
- other tooling/services that need to execute Link pipeline automation workflows

## Notes

- Targets `.NET 8`.
- Most classes are designed to be composed in DI-backed test/service bootstraps.
