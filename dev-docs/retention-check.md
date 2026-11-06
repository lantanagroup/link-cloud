# Retention check

Each night Report finds the reports whose clinical data has aged past the retention window, records a
retention cleanup for them, and asks the participating services to delete that data. This page covers that
check: its settings, which reports it selects, how a run proceeds, and what a failure leaves behind.

Related pages:

- [retention-schedule.md](retention-schedule.md): the Quartz job that fires the check once a day, on one
  Report instance.
- [retention-cleanup-record.md](retention-cleanup-record.md): the cleanup record the check writes, and how
  participants' replies update it.
- [retention-events.md](retention-events.md): the `RetentionCheckRequested` and `RetentionCheckComplete`
  messages.
- [report-retention-dates.md](report-retention-dates.md): the dates a report ages from.

The eligibility rule is the one in
[ADR 08, Data Retention](https://keystone.lantanagroup.com/Proposals/Details/08-data-retention), Workflow
step 1.

## Settings

`RetentionCheckSettings` reads both keys once, when Report starts. The defaults are in code.

| Key                              | Default | Meaning                                                                 |
|----------------------------------|---------|-------------------------------------------------------------------------|
| `Retention:Window`               | `P3M`   | How long clinical data is kept, as one ISO-8601 years, months, weeks or days value. |
| `Retention:MaxReportsPerCleanup` | `25`    | The most reports one cleanup, and so one request message, may hold.     |

`RetentionWindowParser` reads the window:

- Exactly one component: `P<n>Y`, `P<n>M`, `P<n>W` or `P<n>D`. Years become 12 months and weeks 7 days.
  Combined values such as `P1Y6M` and time values such as `PT24H` are rejected.
- At most 36 months or 1095 days after conversion, so `P3Y` and `P156W` are the largest accepted.
- The result is the contract's `RetentionWindow { Unit, Count }`, in months or days. Months stay calendar
  months. Never read the value with `XmlConvert.ToTimeSpan` or `TimeSpan`: both turn `P3M` into a flat
  90 days.

An invalid value never falls back to the default, because retention deletes data. Report still starts:
`RetentionScheduleHostedService` logs the errors at `Error`, and every firing logs an error and does nothing
until the configuration is fixed. With valid settings, startup logs one line such as "Retention checks will
use a window of P3M (3 Months) and at most 25 reports per cleanup."

## Which reports are eligible

A report is eligible when its retention status is `None` and its governing date is strictly before the
cutoff. `RetentionEligibilityQuery` applies that rule:

- The governing date is `CompletedDate` for a report that finished, even if it was soft-deleted later, and
  otherwise `SoftDeletedDate`. Only a report that never finished gets a `SoftDeletedDate`, so the query
  compares `CompletedDate ?? SoftDeletedDate`.
- A report with neither date never finished and was never deleted. It has no clock and is never selected.
- Soft-deleted reports are included. The query deliberately has no `IsDeleted` filter, unlike the other
  report queries.
- A report already in a cleanup is `PendingDeletion` or `Scrubbed`, so it is never selected again. That
  covers reports in an in-progress, partial or failed cleanup.
- A report exactly on the cutoff is not selected.
- Results come oldest first, then by id, up to the cap.

The query only reads. A report it returns can still be taken by another cleanup first; the cleanup manager
claims reports one at a time and skips those.

## The cutoff

`RetentionCutoff.From(now, window)` counts the window back from the moment the check runs:

- `now` comes from `TimeProvider`, not from the firing's scheduled time, which can be a recovery instant.
- It is converted to UTC first, so the offset it arrives with does not matter.
- Months are calendar months. When the day does not exist in the earlier month, the cutoff is that month's
  last day: three months before 31 May is 28 February, or 29 February in a leap year.
- The time of day is kept, so the cutoff is exactly one window before the run.

The cutoff is computed once per run. Every report in the run carries that window and cutoff, and the
request's top-level `CutoffDateTime` is the same value. Re-sends and retries reuse them; they are never
recomputed.

## A run

`RetentionCheck` is the job's `IRetentionCheckWorker`. Each run:

1. Stops with an error log if the settings are invalid.
2. Re-sends unanswered requests through `IRetentionPendingResender`. The registered `NoRetentionResends`
   only logs at `Debug`, because re-sending is not built yet; replace that registration when it is.
   Re-sends come first so an unanswered request does not wait behind a large new backlog. New reports do
   not depend on them, so a re-send failure is logged at `Error` and the run carries on.
3. Computes the cutoff and the run id.
4. Loops until no eligible report is left, checking for cancellation on each pass:
   1. selects up to `Retention:MaxReportsPerCleanup` eligible reports;
   2. records a cleanup for them with `IRetentionCleanupManager.CreateAsync`, which moves each report it
      takes from `None` to `PendingDeletion`;
   3. publishes the request it returns;
   4. records that the request was sent with `RecordSentAsync`.
5. Logs one `Information` summary: the cutoff, the window, cleanups requested, reports included, and
   reports skipped because another cleanup took them first.

Each pass takes the reports it selects out of `None`, so the next query moves on to the following reports
without any paging. A backlog larger than the cap becomes several cleanups in one run, one after another.

Every scheduled request carries `InitiatedBy = Scheduled`, `Mode = Scrub` and no `Participants` list, which
means every expected participant. The cleanup record stores `SystemUser` as the initiator; the initiator is
never sent on Kafka.

The run is traced as a `RetentionCheck.Run` span, with the run id, cutoff and counts as tags, inside the
job's own span.

## The run id

`RetentionRunIds.For(scheduledFireTime)` is a version 5 name-based UUID of
`retention-check|<fire time in UTC>`, built with the shared `NameBasedGuid`. A run recovered on another
instance gets the original firing's time from the job (see
[retention-schedule.md](retention-schedule.md), *Fire time*), so its cleanups share the crashed run's id and
group with them in the cleanup history.

The id only groups cleanups. It is predictable, because its namespace is in this repository, so it must never
decide access. Nothing about a cleanup's safety depends on it.

## When a run stops early

| What happens | What the run does | What is left behind |
|---|---|---|
| The settings are invalid | Logs an error and does nothing. | Nothing. |
| `Retention:Participants` is invalid | The manager records the cleanup `Failed` with the reason and sends nothing; the run logs a warning and stops, since every further batch would fail the same way. | One `Failed` cleanup; its reports stay `None` and are selected again once the list is fixed. |
| A report is selected a second time in one run | Logs an error and stops. This should never happen; it guards against looping forever. | Whatever the run had finished. |
| Publishing fails | Logs an error naming the cleanup and rethrows; the job logs the failure and waits for the next firing. | The cleanup is recorded with no send time, and its reports are `PendingDeletion`, so no later run selects them; the re-send picks the cleanup up. |
| Recording the send fails after a successful publish | The exception reaches the job. | The participants have the request, but it is recorded as unsent, so a re-send asks again. Participants answer a repeated request harmlessly. |
| The run is canceled | Stops between batches. | Every cleanup finished so far is recorded and sent. |

## Replays, recovery and concurrency

Only one retention run happens at a time across all Report instances: the job is
`[DisallowConcurrentExecution]` in a clustered Quartz store (see [retention-schedule.md](retention-schedule.md)).

A replayed or recovered firing cannot clean a report twice. The cleanup manager takes a report only while
it is `None`, in a single `UPDATE`, so a report already in a cleanup is skipped. A run that crashed after
recording a cleanup but before publishing it leaves that cleanup unsent rather than lost, and the recovery
does not select its reports again.

Manual and single-report cleanups are not built yet. When they are, they must create their cleanups through
the same `CreateAsync`, so that if one runs at the same time as the scheduled check, whichever claims a
report first takes it and the other skips it.

## Publishing

`RetentionCheckRequestedProducer` publishes on `RetentionCheckRequested`, keyed by the `RetentionCleanupId`
with the same value in the `X-Correlation-Id` header. It waits for the broker to acknowledge each message and
throws on a failed delivery, so a request is never recorded as sent unless Kafka has it. The producer is
created the first time it is needed, from the `KafkaConnection` Report already registers, with the client id
`Report_RetentionCheckRequested`. `AddRetentionSchedule` registers the producer, the publisher, the worker
and the re-send placeholder.

## Tests

| Test class | Covers |
|---|---|
| `UnitTests.Report.RetentionWindowParserTests` | Every unit, the limits, malformed values. |
| `UnitTests.Report.RetentionCheckSettingsTests` | Defaults, invalid values, both keys invalid together. |
| `UnitTests.Report.RetentionCutoffTests` | Month ends, leap days, day counts, offsets. |
| `UnitTests.Report.RetentionRunIdsTests` | Same firing gives the same id; a pinned id. |
| `UnitTests.Shared.NameBasedGuidTests` | Values from an independent version 5 implementation. |
| `UnitTests.Report.RetentionCheckTests` | Call order, the request's fields, batching, every early stop, cancellation. |
| `UnitTests.Report.RetentionCheckRequestedProducerTests` | Topic, key, header, delivery failures. |
| `IntegrationTests.Report.Managers.RetentionEligibilityQueryTests` | The eligibility rule on SQL Server. |
| `IntegrationTests.Report.RetentionCheckTests` | Whole runs on SQL Server: batching, replay and recovery, a failed publish, invalid participants, and the next run after participants complete or fail a cleanup. |

The integration tests share one database with the rest of the collection, and the check reads every
facility, so each test works in its own year long before any real data and asserts only on its own reports.
