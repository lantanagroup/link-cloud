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

## Data Acquisition producer

Data Acquisition emits `PatientIdentified` when it receives an initial `DataAcquisitionRequested`,
before creating acquisition logs. Supplemental requests do not repeat that event. The original
request occurrence time is retained in `X-Acquisition-Identified-At` across listener retries.

Once every sibling log in a facility/correlation/phase group is terminal, the inline worker or tail
recovery emits `InitialAcquisition` or `SupplementalAcquisition` for every distinct scheduled report.
`MaxRetriesReached`, `ConfigurationMissing`, `Cancelled`, and `AccessDenied` are failed outcomes:
the event lists their log IDs and **no `ResourcesAcquired` is produced**. Retryable `Failed` logs do
not complete a phase. `Completed`, `Skipped`, and `NotReportable` retain the successful handoff.
Missing configuration creates terminal logs stamped for recovery, even though no worker runs them.
Cancellation and scheduler-side terminal updates also check their affected groups immediately. If
they finish a phase containing a failed reference log, the failure is reported without waiting for
the recovery job's minimum age. A failed status send leaves the durable log changes intact and the
tail available for recovery.

The completion timestamp comes from the latest log completion time (or last modification for a
terminal log without one). Tail claims and claim release preserve that occurrence time.
`processingTime`, when available, sums recorded log processing durations, excluding queue wait.
Both outcome and downstream sends must be acknowledged before the group is marked `TailSent`;
publication errors leave the group recoverable. Duplicate status delivery remains possible across
recovery and is handled by Report's timestamp/transition checks. A report being canceled does not
suppress acquisition outcomes: Report owns terminal-state protection.

The status producer uses the canonical report/patient key and Murmur2 partitioning, and propagates
correlation and metrics-mode headers. Census/non-reporting phases do not emit patient status events.

## Normalization producer

Normalization emits `InitialNormalization` or `SupplementalNormalization` on `ReportStatusUpdated`
for each distinct `ScheduledReports.ReportTrackingId` and the `ResourcesAcquired` patient. Phase
names are case-insensitive. Success means the entire cached bundle was processed, including an
empty bundle or resources with no configured operations. Nonfatal operation/mapping outcomes retain
their existing behavior and do not fail the processing stage.

The success event is acknowledged before `ResourcesNormalized` is published or acquisition cache
keys are deleted. Its duration measures normalization work, excluding consumer lag and event sends.
`X-Normalization-Completed-At` and `X-Normalization-Processing-Time` on the source message retain the
original occurrence time and duration across retries of that publication.
After the downstream handoff succeeds, source-cache cleanup failures are logged and left to cache
expiration rather than retried as a failed normalization stage.

Direct dead letters and exhausted acquisition-message retries emit a failure for the failed phase;
temporary failures and pipeline aborts/shutdown cancellation do not. Exhausted retries preserve the
original error message when available. Null correlation-header values are treated as missing so
malformed events can still be dead-lettered and their cache released. Failure status sends are best
effort after Kafka's producer
retries, so a diagnostic-send failure is logged and terminal dead-letter/cache cleanup continues.
Messages lacking a patient id, correlation id, report id, or recognized phase cannot emit a valid
status event. The status producer uses the canonical report/patient key and Murmur2 partitioning,
and propagates the correlation and metrics-mode headers.

## MeasureEval producer

MeasureEval emits `InitialEvaluation` or `SupplementalEvaluation` once per distinct report/patient
after evaluation, persistence, and the downstream handoff have succeeded. Both
`DataAcquisitionRequested` (initial reportable patients) and `MeasureReportGenerated` sends wait for
Kafka acknowledgement; an asynchronous send failure therefore follows the inbound retry path.
Initial events include `reportable`, true if any measure in that report is reportable. Supplemental
events omit it and exclude reports already completed as not reportable during initial evaluation.
An empty initial bundle retains the existing successful, not-reportable outcome.

Regeneration (`EvaluationRequested`) performs reportability and final evaluation together without
acquisition. After its downstream handoffs it reports initial completion and, for reportable reports,
supplemental completion against the new report id and workflow correlation id.

Durations exclude consumer lag. `X-Evaluation-Completed-At-<phase>` and
`X-Evaluation-Processing-Time-<phase>` preserve the original successful occurrence and duration when
status publication is retried. Success is published before cache cleanup so failed status sends
retain the resources required for retry.

Direct dead letters, disabled retries, and exhausted retries report `isSuccess=false` for the failed
evaluation phase. Temporary failures emit no failure status. The terminal hook runs after durable
dead-letter publication and still releases the cache if status publication fails. As with the shared
recoverer, terminal diagnostic publication is best effort after Kafka producer retries. Failures
include the exception type and its message, sanitized with the shared `LogUtils` helper. When the
message is absent or blank, only the exception type is included.
Malformed messages without report/patient/correlation identity cannot emit a valid status event.
Status records use the canonical key, default Java key hashing, and correlation/metrics-mode headers.

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
