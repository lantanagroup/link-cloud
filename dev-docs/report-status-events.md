# Report status event contract

`ReportStatusUpdated` reports completion or failure of a **patient's** processing stage. Report consumes
these events and advances the patient to the next stage on success (for example,
`InitialAcquisition` completion advances to `InitialNormalization`). Report derives the report lifecycle
from patient statuses; report-level statuses are never sent in this event.

Source: [LEGLINK-1220 event contract](https://lantana.atlassian.net/wiki/spaces/LSD/pages/2372993025).
LEGLINK-1223 introduces the shared contracts only. Producers, consumers, transitions, persistence, and
replacement of existing status enums are implemented in subsequent stories on the LEGPROG-497 branch.

## Topics and models

The topic has the standard `ReportStatusUpdated-Retry` and `ReportStatusUpdated-Error` companions.
All three are registered in `topics.txt`, .NET `KafkaTopic`, and Java `Topics`.

| Contract | .NET Shared | Java shared (`kafka.records.reportstatus`) |
| --- | --- | --- |
| Patient lifecycle | `Application.Enums.Reporting.PatientReportingStatus` | `PatientReportingStatus` |
| Report lifecycle | `Application.Enums.Reporting.ReportStatus` | `ReportStatus` |
| Event value | `Application.Models.Kafka.ReportStatusUpdatedValue` | `ReportStatusUpdated` |
| Validation outcome | `Application.Models.Kafka.ValidationResult` | `ValidationResult` |

Both runtimes serialize enums as the PascalCase strings in the contract. Java constants use standard
uppercase names with explicit Jackson wire names. JSON properties use camelCase, with explicit wire
names on the .NET models; the shared Kafka consumers accept PascalCase properties as well.

## Payload

| Field | Type | Meaning |
| --- | --- | --- |
| `reportScheduleId` | required string | Report identifier |
| `patientId` | required string | Patient identifier; events are always patient-specific |
| `status` | required PatientReportingStatus | Stage that completed or failed |
| `isSuccess` | required boolean | Whether that stage completed successfully |
| `processingTime` | optional 64-bit integer | Stage duration in milliseconds, excluding consumer lag |
| `errorMessage` | optional string | Failure details when `isSuccess` is false |
| `correlationId` | required string | Workflow tracing identifier |
| `validationResult` | optional object | Populated by Validation: `passed` (boolean), `messageCount` (64-bit integer) |
| `reportable` | optional boolean | Populated on InitialEvaluation; true if any measure is reportable |
| `timestamp` | required ISO 8601 UTC timestamp | Occurrence time, preserved on retries |

Absent optional fields are omitted. Explicit `false` and zero values are preserved. `messageCount`
counts all validation results, including all categories and severities. A validation outcome is separate
from whether the validation *stage* ran successfully. There is no facility id, service name, report-level
status, or FHIR resource body in the value.

The patient lifecycle values are `PatientIdentified`, `InitialAcquisition`, `InitialNormalization`,
`InitialEvaluation`, `SupplementalAcquisition`, `SupplementalNormalization`, `SupplementalEvaluation`,
`Validation`, `Submission`, `Submitted`, `Failed`, `NotReportable`, and `NotSubmitted`.
Report lifecycle values are `Pending`, `Completed`, `CompletedNotSubmitted`, `Failure`, and `Canceled`.

The Confluence summary mentions `AcquisitionRequested`, but its detailed enum and payload definitions
do not define that value. These models follow the detailed definitions, retaining `PatientIdentified`.

## Contract checks

The fixtures in `Java/shared/src/test/resources/report-status/` are shared with .NET ServiceTests.
Both runtimes check the same payloads and enum names, nullable values, explicit false/zero values,
64-bit counts, and occurrence timestamps after deserialization and republication. Run:

```powershell
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~ReportStatusUpdatedContractTests
```

```sh
cd Java
mvn -pl shared -am test -Dtest=ReportStatusUpdatedContractTest -Dsurefire.failIfNoSpecifiedTests=false
```
