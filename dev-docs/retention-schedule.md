# Retention schedule

Report runs the retention check from a daily Quartz job, `RetentionCheckJob`, in its existing
SQL-clustered Quartz store. The job only schedules the work. It hands each firing to
`IRetentionCheckWorker`, and what that worker does is
[LEGLINK-1211](https://lantana.atlassian.net/browse/LEGLINK-1211)'s. The ticket for the job is
[LEGLINK-1565](https://lantana.atlassian.net/browse/LEGLINK-1565), the Dev sub-task of
[LEGLINK-1564](https://lantana.atlassian.net/browse/LEGLINK-1564). The events the check publishes
are covered in [retention-events.md](retention-events.md), and the dates it ages reports from in
[report-retention-dates.md](report-retention-dates.md).

Status: the settings, the job and the worker interface are in place. The startup step that stores
the job and its trigger in the Quartz store is still to come, so nothing fires the job yet. Update
this page when it lands.

## Settings

`RetentionScheduleSettings` binds `Retention:Schedule`. Both keys have defaults in
`appsettings.json`.

| Key | Default | Meaning |
| --- | --- | --- |
| `Retention:Schedule:Cron` | `0 0 2 * * ?` | Quartz cron expression, evaluated in UTC. The default is 02:00 UTC daily. |
| `Retention:Schedule:Enabled` | `true` | Whether the job is scheduled at all. |

`CronIsValid` is false for a blank value or one Quartz cannot parse, and `ResolvedCron` then
returns the default instead. Settings are read with `IOptions`, so a change takes effect at the
next startup.

## The job

`RetentionCheckJob` is `[DisallowConcurrentExecution]`. Report's Quartz store is clustered, so one
firing runs on one Report instance, and a second firing waits for the first to finish rather than
running beside it.

Each firing:

1. builds a `RetentionCheckRun` from the Quartz context (see *Fire time* below);
2. logs that it started;
3. creates a DI scope, resolves `IRetentionCheckWorker` from it, and calls `RunAsync` with the
   job's cancellation token;
4. logs that it finished, with the elapsed time.

The job is built by the job factory `AddQuartz` registers, which resolves its constructor from the
container, so it is not registered in DI itself.

## The worker interface

`IRetentionCheckWorker.RunAsync(RetentionCheckRun run, CancellationToken cancellationToken)` is the
hand-off to LEGLINK-1211. The default registration is `LoggingRetentionCheck`, which logs one
`Information` line saying no retention check is implemented and does nothing else. LEGLINK-1211
replaces that registration in `Program.cs`.

`RetentionCheckRun` carries:

| Property | Value |
| --- | --- |
| `ScheduledFireTimeUtc` | The time the firing was due. See *Fire time* for when this is not the cron time. |
| `IsRecovery` | True when this run replaces one interrupted by a Report instance that died. |
| `FireInstanceId` | Quartz's id for this firing. A recovery run gets a new one. |

It is a record rather than loose parameters, so LEGLINK-1211 can add a field without changing the
interface.

## Fire time

`ScheduledFireTimeUtc` is the cron time on an ordinary firing. Two kinds of late run differ.

**Cluster recovery.** An instance dies partway through a run. Quartz notices its check-ins have
stopped and re-runs the job on another instance with `Recovering` set. The re-run's own scheduled
time is the recovery instant, but Quartz writes the original firing's time into the job data map
under `SchedulerConstants.FailedJobOriginalTriggerFiretime`, formatted with the invariant culture.
The job reads it back, so `ScheduledFireTimeUtc` is the time the interrupted run was due. If the
value is missing or cannot be parsed, the job falls back to the recovery instant.

Quartz 3.18.1 never writes `FailedJobOriginalTriggerScheduledFiretime`, although the constant
exists, so the job does not read it.

**Misfire.** Every instance is down at the cron time, so nothing fires. The trigger's misfire
policy is fire-once-now: when an instance comes back it runs the job once, not once per missed day.
Quartz does that by moving the trigger's next fire time to the recovery instant, so the firing
reports that instant as its scheduled time, and `Recovering` is false. Nothing records the missed
cron time. `DmrpNightlyJob` has the same behavior and describes it in its remarks.

For retention this is acceptable, because the check ages reports against the date it runs, not
against 02:00. LEGLINK-1211 must not treat `ScheduledFireTimeUtc` as an exact cron-time anchor,
for example to compute a cutoff.

## Failures

A cancellation from the job's token propagates. Any other exception is logged at `Error`, marked on
the job's span, and rethrown as a `JobExecutionException` with `RefireImmediately = false`. A
failing check therefore waits for the next day's firing instead of looping.
