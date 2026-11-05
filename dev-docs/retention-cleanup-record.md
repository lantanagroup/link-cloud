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

## `Retention:Participants`

Each entry is a `ServiceName`, the name the participant puts in `RetentionCheckComplete`, and a `Scope`:

- `Report`: the participant deletes per report and replies with one result per report.
- `Cutoff`: the participant acts on the cleanup's cutoff and replies with one result per cleanup. It never
  holds back a report's `Scrubbed` state.

When the key is not set, the list is `Report`, `MeasureEval`, `Validation` and `DataAcquisition`, all
`Report`-scoped. In App Configuration a custom list is one pair of rows per entry:

```text
Retention:Participants:0:ServiceName = Report
Retention:Participants:0:Scope       = Report
Retention:Participants:1:ServiceName = MeasureEval
Retention:Participants:1:Scope       = Report
```

Set the whole list or none of it. .NET merges list entries from different sources by index, so rows for
indices 0 and 1 on top of a four-entry list elsewhere would leave entries 2 and 3 in place. That is why the
default is in code and not in `appsettings.json`.

The whole list is invalid when it is empty (including an empty JSON array), when an entry has no `ServiceName`
or no `Scope`, when a `Scope` is not `Report` or `Cutoff`, when a name is longer than 100 characters, or when
two entries share a name, ignoring case. Report reads the list once at startup and logs every problem as an
error, but still starts. While the list is invalid, each cleanup is recorded `Failed` with the reason, no
request is sent, and its reports stay at `None`. A change takes effect when Report restarts.

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

## Creating a cleanup

`IRetentionCleanupManager.CreateAsync` records a cleanup. The caller (the retention check, LEGLINK-1273)
computes everything first: the reports with the window and cutoff that selected each, the top-level cutoff,
`InitiatedBy`, `Mode`, the run id and the initiating user. In one transaction, the manager:

1. Takes the requested reports that are still `None`, moving them to `PendingDeletion`. It does this with a
   single `UPDATE ... OUTPUT` that only touches rows still at `None`. A second cleanup asking for the same
   report waits on the row lock, then finds the report no longer `None` and skips it, so a report is never in
   two cleanups that send requests.
2. Writes the cleanup (`InProgress`), its reports, a copy of `Retention:Participants`, and a `Pending` result
   for each report-scoped participant and report, plus one for each cutoff-scoped participant.

It returns the cleanup id, the reports it took, the reports it skipped (missing, or no longer `None`) and the
`RetentionCheckRequested` value to publish.

When `Retention:Participants` is invalid, it takes nothing. The cleanup is recorded `Failed` with the reason
and the reports it would have covered, which stay at `None`, and there are no participants, no results and
nothing to publish. When none of the requested reports is still `None`, no cleanup is recorded at all.

A request that breaks a contract rule throws `ArgumentException`:

- no reports, or the same report twice;
- `Purge` on anything but a single-report cleanup;
- a single-report cleanup with more than one report, or with a window;
- a scheduled or manual cleanup with a report that has no window;
- no initiating user, or a report with no facility.

Sending is recorded separately. After the publish succeeds, the caller calls `RecordSentAsync`, which sets
`LastSentDateTime` and adds one to `SendCount` on every result of the cleanup. If Report stops between the
commit and the publish, the results keep a null `LastSentDateTime`, so re-sending (LEGLINK-1217) can treat
them as never sent.

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
