# Retention cleanup record

Report keeps one record per retention cleanup: the reports it covers, the participants expected to reply,
and a result for each participant and report. Each report also carries its own retention state. None of this
is sent on Kafka. The messages are in [retention-events.md](retention-events.md); the design is the
*Report's cleanup record* section of the approved
[LEGLINK-1242 contract](https://lantana.atlassian.net/wiki/spaces/LSD/pages/2339635201), built under
LEGLINK-1212.

## Tables

All four are in the `link-report` database. Enums are stored as strings.

### `RetentionCleanup`

One row per cleanup. `Id` is the `RetentionCleanupId` on the wire.

| Column                                     | Meaning                                                                                        |
|--------------------------------------------|------------------------------------------------------------------------------------------------|
| `Id`                                       | The `RetentionCleanupId`, kept for retries and re-sends.                                       |
| `RunId`                                    | The run that created the cleanup. One large run can split into several cleanups that share it. |
| `InitiatedBy`                              | `Scheduled`, `Manual` or `SingleReport`.                                                       |
| `InitiatedDateTime`                        | When Report created the cleanup, in UTC.                                                       |
| `InitiatedByUserId`, `InitiatedByUserName` | `SystemUser` for a scheduled run, otherwise the person who started it.                         |
| `Mode`                                     | `Scrub` or `Purge`.                                                                            |
| `CutoffDateTime`                           | The cutoff for the system default window, used by cutoff-scoped participants.                  |
| `Status`                                   | `InProgress`, `Completed`, `Partial` or `Failed` (see *Status rules*).                         |
| `FailureReason`                            | Why no request was sent, such as an invalid participant list. Null otherwise.                  |
| `CompletedDateTime`                        | When `Status` became `Completed`. Null until then.                                             |

### `RetentionCleanupReport`

One row per report in a cleanup, with the window and cutoff that selected it. The window columns are null on a
single-report cleanup, which no window selected.

`ReportScheduleId` is **not** a foreign key to `ReportSchedule`. A purge deletes the report's own rows, and
the cleanup that did it has to stay in the history afterward.

### `RetentionCleanupParticipant`

The expected participants, copied from `Retention:Participants` when the cleanup is created: a
`ServiceName` and a `Scope` (`Report` or `Cutoff`) each. A later configuration change does not touch cleanups
that already exist. A completion whose `ServiceName` is not listed here is logged and ignored.

### `RetentionCleanupResult`

One row per report-scoped participant and report, and one per cleanup, with a null
`RetentionCleanupReportId`, for a cutoff-scoped participant.

| Column                          | Meaning                                                                                             |
|---------------------------------|-----------------------------------------------------------------------------------------------------|
| `Status`                        | `Pending` until a reply arrives, then `Completed` or `Failed`. `Pending` is never sent on the wire. |
| `RecordsAffected`               | From the latest reply. On `Failed`, the count deleted before the failure. Null while pending.       |
| `ErrorMessage`                  | The participant's sanitized description. No stack trace and no PHI.                                 |
| `CompletedDateTime`             | When the participant finished the result, whatever the outcome.                                     |
| `LastSentDateTime`, `SendCount` | When the request was last sent and how many times. Drive re-sending (LEGLINK-1217).                 |

The unique index on (`RetentionCleanupParticipantId`, `RetentionCleanupReportId`) is configured with no
filter. EF Core would otherwise add `IS NOT NULL` to a unique index on a nullable column, which would let a
cutoff-scoped participant have more than one result. Without the filter, SQL Server treats the null as a
value and allows one.

Deleting a cleanup cascades to its reports and participants, and from participants to results. The
results' foreign key to the report has no cascade, because SQL Server rejects two cascade paths to one
table.

### `ReportSchedule.RetentionStatus`

| Value             | When                                                                                             | Clinical data can be downloaded |
|-------------------|--------------------------------------------------------------------------------------------------|---------------------------------|
| `None`            | Never in a cleanup. The default, and the only value the eligibility rule selects.                | yes                             |
| `PendingDeletion` | From the moment a cleanup includes the report, through any in-progress, partial or failed state. | no                              |
| `Scrubbed`        | Every expected report-scoped participant has returned `Completed` for it.                        | no                              |

Cutoff-scoped participants never hold back `Scrubbed`. A purged report has no state, because its row is gone.

## Status rules

`RetentionCleanup.Status` is recalculated on every reply, across all of the cleanup's results:

| Status       | When                                                              |
|--------------|-------------------------------------------------------------------|
| `InProgress` | At least one result is `Pending`, and none is `Failed`.           |
| `Completed`  | Every result is `Completed`.                                      |
| `Partial`    | At least one result is `Failed`, and at least one is `Completed`. |
| `Failed`     | At least one result is `Failed`, and none is `Completed`.         |

A failure shows as soon as it arrives. A later success moves `Failed` to `Partial`, and a successful retry
moves `Partial` to `Completed`.
