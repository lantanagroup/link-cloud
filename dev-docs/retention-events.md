# Retention events

Data retention runs on two Kafka topics. Report sends `RetentionCheckRequested` to every participant,
and each participant answers with one `RetentionCheckComplete`. The contract, meaning every field and
the rules participants and Report follow, belongs in
[ADR 08, Data Retention](https://keystone.lantanagroup.com/Proposals/Details/08-data-retention). Until
the ADR is updated, the approved version (2026-10-19) is the PDF attached to
[LEGLINK-1242](https://lantana.atlassian.net/browse/LEGLINK-1242), "LEGLINK-1242 - Proposed Retention
Event Contract". This page covers only where the types live and how to use them. The topics and types
were added by [LEGLINK-1204](https://lantana.atlassian.net/browse/LEGLINK-1204).

## Topics

| Topic                     | Key                           | Producer          | Consumer          |
|---------------------------|-------------------------------|-------------------|-------------------|
| `RetentionCheckRequested` | `RetentionCleanupId` (string) | Report            | every participant |
| `RetentionCheckComplete`  | `RetentionCleanupId` (string) | every participant | Report            |

Each has `-Retry` and `-Error` companions in `topics.txt`, in `KafkaTopic` (.NET) and in `Topics`
(Java). Keying on the cleanup id puts every message for one cleanup on one partition.

The older `RetentionCheckScheduled` topic and Tenant's `RetentionCheckScheduledJob` are an earlier,
unfinished attempt and play no part in this.

## Types

| Message or part           | .NET (`Shared.Application.Models.Retention`) | Java (`shared.kafka.records.retention`) |
|---------------------------|----------------------------------------------|-----------------------------------------|
| `RetentionCheckRequested` | `RetentionCheckRequestedValue`               | `RetentionCheckRequested`               |
| `RetentionCheckComplete`  | `RetentionCheckCompleteValue`                | `RetentionCheckComplete`                |
| One report in a request   | `RetentionReport`                            | `RetentionReport`                       |
| One result in a reply     | `RetentionReportResult`                      | `RetentionReportResult`                 |
| Retention window          | `RetentionWindow`                            | `RetentionWindow`                       |
| Enums                     | `RetentionInitiatedBy`, `RetentionMode`, `RetentionResultStatus`, `RetentionWindowUnit` | same names |

The .NET message types carry the `Value` suffix the other .NET message values use; the Java ones are
named after their topic, as the other Java records are. The Java types live in `Java/shared` because
MeasureEval and Validation both need them.

## What is on the wire

- Property names are PascalCase from .NET and camelCase from Java. Both sides match them ignoring case,
  so neither side renames anything.
- Enums are strings: `Scrub`, not `0`. Each .NET enum carries `JsonStringEnumConverter`, and each Java
  constant pins its wire name with `@JsonProperty`, so `SINGLE_REPORT` goes out as `SingleReport`.
- Timestamps are ISO-8601 in UTC with an offset. .NET writes `+00:00` and Java writes `Z`; both read
  either.
- Optional fields that have no value are left out rather than sent as `null`. That covers a request's
  `Participants`, a SingleReport request's `RetentionWindow`, a reply's `Reports` or `Summary`, and a
  result's `ReportScheduleId` and `ErrorMessage`.
- The .NET types mark the contract's required fields `required`. A .NET producer that leaves one out
  does not compile, and a message missing one fails to deserialize.

## Producing and consuming

- **.NET:** produce with `JsonWithFhirMessageSerializer<T>` and consume with
  `JsonWithFhirMessageDeserializer<T>`, as the other topics do. The serializer uses System.Text.Json,
  which honors the enum converters.
- **Java:** register a typed `JsonDeserializer` for each retention topic, built with the service's
  Spring `ObjectMapper` bean, as `KafkaConfig` does for the existing topics. Reading the PascalCase
  names relies on that bean's `accept-case-insensitive-properties` and `accept-case-insensitive-enums`
  settings from `application.yml`. A plain `new ObjectMapper()` lacks them and reads every field as
  null without raising an error. Produce with a `JsonSerializer` built from the same bean, with
  `noTypeInfo()`.

## Tests and fixtures

The same JSON fixtures are checked by both runtimes, so a change on one side that the other can't read
fails a build:

- `Java/shared/src/test/resources/retention/*.json` holds the fixtures. `ServiceTests.csproj` links
  them into its output, so there is one copy.
- `retention-check-requested-*.json` is what .NET produces. `RetentionMessageContractTests` (.NET)
  asserts its output matches them exactly, and `RetentionMessageContractTest` (Java) reads them.
- `retention-check-complete-*.json` is what Java produces. The Java test asserts its output matches
  them exactly, and the .NET test reads them.

To change a field, change both runtimes' types and the fixture together, and run both test suites.

## Creating the topics

`topics.txt` is the source for every environment this repository deploys:

- **Locally**, compose's `kafka_init` service creates any missing topic through the REST Proxy when the
  stack comes up.
- **DEV, TEST, QA, QA2 and QA3** are synced by `Azure_Pipelines/kafka-topics-sync.yaml`, which is run by
  hand once per environment and reads `topics.txt` from the branch it runs on. It creates missing topics,
  applies their configs, and deletes any topic the file doesn't list. The topics exist in an environment
  only after someone has run it there, so new topics go in the release notes.
- **CDC environments** are deployed from a separate repository and need the topics there too.

## Topic retention

`RetentionCheckRequested` has to keep messages longer than the longest participant outage we expect;
otherwise a request can expire before a participant reads it. Without a setting it gets the broker
default, 7 days on the local stack.

A row in `topics.txt` can carry a fourth field of topic configs, comma-separated `key=value` pairs, such
as `RetentionCheckRequested:3:1:retention.ms=<milliseconds>`. The sync pipeline applies it to new and
existing topics. Locally, `create-topics-rest.sh` does not send that field, so local topics keep the
broker default.
