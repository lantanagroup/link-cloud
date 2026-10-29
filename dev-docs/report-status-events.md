# Report status event contract

`ReportStatusUpdated` reports completion or failure of a **patient's** processing stage. Report consumes
these events and advances the patient to the next stage on success (for example,
`InitialAcquisition` completion advances to `InitialNormalization`). Report derives the report lifecycle
from patient statuses; report-level statuses are never sent in this event.

The shared contracts define the event format. Service integration, including producers, consumers,
transitions, persistence, and replacement of existing status enums, is separate work.

## Topics and models

The topic has the standard `ReportStatusUpdated-Retry` and `ReportStatusUpdated-Error` companions.
All three are registered in `topics.txt`, .NET `KafkaTopic`, and Java `Topics`.

| Contract | .NET Shared | Java shared (`kafka.records.reportstatus`) |
| --- | --- | --- |
| Patient lifecycle | `Application.Enums.Reporting.PatientReportingStatus` | `PatientReportingStatus` |
| Report lifecycle | `Application.Enums.Reporting.ReportStatus` | `ReportStatus` |
| Event key | `Application.Models.Kafka.ReportStatusUpdatedKey` | `ReportStatusUpdatedKey` |
| Event value | `Application.Models.Kafka.ReportStatusUpdatedValue` | `ReportStatusUpdated` |
| Validation outcome | `Application.Models.Kafka.ValidationResult` | `ValidationResult` |

Both runtimes serialize enums as the PascalCase strings in the contract. Java constants use standard
uppercase names with explicit Jackson wire names. JSON properties use camelCase, with explicit wire
names on the .NET models; the shared Kafka consumers accept PascalCase properties as well.

## Key and partitioning

Every `ReportStatusUpdated` record must use a `ReportStatusUpdatedKey` containing **both** `reportId`
and `patientId`. `reportId` is copied from the value's `reportScheduleId`, and `patientId` from its
`patientId`. Do not add facility, correlation, stage, or service identifiers to the key: updates from
every service for the same report/patient pair need the same key. Preserve it on retry/error records.

The canonical UTF-8 JSON key is compact, camelCase, with `reportId` before `patientId`:

```json
{"reportId":"0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803","patientId":"patient-123"}
```

The key uses the report identifier and FHIR patient id as supplied by the workflow. Producers must
serialize consistently, without indentation, casing changes, or additional fields. The shared fixture
checks the exact bytes produced by .NET's Kafka serializer and Java's Spring Kafka JSON serializer.

When integrating this contract into services, .NET producers must set
`ProducerConfig.Partitioner = Partitioner.Murmur2Random` to match Java's default keyed partitioning.
Java producers must retain key hashing (do not enable `partitioner.ignore.keys`). Identical key objects
alone are insufficient: Kafka hashes the serialized bytes, and .NET/librdkafka's default CRC32 hash
differs from Java's Murmur2 hash. See [Confluent's partitioner guidance](https://docs.confluent.io/platform/current/clients/client-configs.html).
This requirement applies to the new status topic; existing topic partitioning is unaffected.

With a stable partition count, matching key bytes and partitioners route each report/patient pair to
one partition, consumed by one member of Report's consumer group at a time. Kafka preserves log order
within that partition; retries on a separate topic can still arrive after later stage updates, so the
consumer must retain timestamp/transition checks.

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

## Contract checks

The fixtures in `Java/shared/src/test/resources/report-status/` are shared with .NET ServiceTests.
Both runtimes check the same payloads and enum names, nullable values, explicit false/zero values,
64-bit counts, occurrence timestamps after deserialization and republication, and exact serialized key
bytes. Run:

```powershell
dotnet test DotNet/ServiceTests/ServiceTests.csproj --filter FullyQualifiedName~ReportStatusUpdatedContractTests
```

```sh
cd Java
mvn -pl shared -am test -Dtest=ReportStatusUpdatedContractTest -Dsurefire.failIfNoSpecifiedTests=false
```
