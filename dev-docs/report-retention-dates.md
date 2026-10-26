# Report retention dates

Retention cleanup ages a report from a date that cannot drift. `ReportSchedule` carries two columns
for that: `CompletedDate`, when the report first finished, and `SoftDeletedDate`, when a report that
never finished was soft-deleted. This page covers who writes them, the rule they follow, and how
rows from before the columns existed got their values. The ticket is
[LEGLINK-1245](https://lantana.atlassian.net/browse/LEGLINK-1245), the Dev sub-task of
[LEGLINK-1209](https://lantana.atlassian.net/browse/LEGLINK-1209). The selection that reads them is
[LEGLINK-1211](https://lantana.atlassian.net/browse/LEGLINK-1211).

## The rule

The rule is the one approved on
[LEGLINK-1243](https://lantana.atlassian.net/wiki/spaces/LSD/pages/2358804481) and carried into
[ADR 08](https://keystone.lantanagroup.com/Proposals/Details/08-data-retention), workflow step 1.
Neither `SubmitReportDateTime` nor `ModifyDate` could carry it: the first is empty for a report
that finished without submitting, and the second moves on every later edit.

- A report that reached `Submitted` or `CompletedNotSubmitted` ages from `CompletedDate`, even if
  it is soft-deleted later. The date is set the first time the status becomes terminal and is never
  moved, so a regenerate that resubmits a report does not restart its clock.
- A report that never finished ages from `SoftDeletedDate`. One that is never soft-deleted has no
  clock at all and is never selected; [LEGLINK-1530](https://lantana.atlassian.net/browse/LEGLINK-1530)
  warns when such a report is stuck.
- Restoring a soft-deleted report clears `SoftDeletedDate`. Deleting it again records the new date.

Both columns are `datetimeoffset(7)` and always hold UTC, like `ReportStartDate` and
`ReportEndDate`. That keeps the UTC-ness in the type, serializes with an offset on the API, and
lets the selection compare them directly against a `DateTimeOffset` cutoff.

## Who writes them

Every write goes through `ReportScheduledManager`; nothing else updates `ReportSchedule`.

| Path                                                                                                                   | `CompletedDate`                                                   | `SoftDeletedDate`                                                       |
|------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------|-------------------------------------------------------------------------|
| `UpdateAsync`, the only route by which `PayloadSubmittedListener` and `ReportManifestProducer` write a terminal status | set to now if the status is terminal and the column is still null | untouched                                                               |
| `SoftDeleteByReportTrackingIdAsync`                                                                                    | untouched                                                         | set to now, unless the status is terminal                               |
| `RestoreByReportTrackingIdAsync`                                                                                       | untouched                                                         | cleared                                                                 |
| `UpdateReportsDeletedStatusForFacility`, delete                                                                        | untouched                                                         | set to now on the `Scheduled` reports; the terminal batches get nothing |
| `UpdateReportsDeletedStatusForFacility`, restore                                                                       | untouched                                                         | cleared on every report it touches                                      |

The two private helpers at the bottom of the manager, `StampCompletedDate` and
`StampSoftDeletedDate`, hold the conditions, so a new write path only has to call them.
`UpdateAsync` never copies either column from the incoming model; `ReportScheduleModel` and
`ReportScheduleApiModel` expose them read-only.

The facility-wide delete skips reports still in progress (`New`, `EndOfPeriod`), as it always has.
Under the rule above such a report gets no clock, so a deleted facility's in-flight reports are
never selected. The single-report delete reaches them through `allowInProgress`. This is a known gap
for a follow-up, not something this page's code changes.

## Rows from before the columns existed

Migration `20261023172257_AddReportCompletedAndSoftDeletedDates` adds the columns and backfills in
the same step. The source columns are `datetime2` in UTC, which SQL Server converts to
`datetimeoffset` at +00:00.

| Rows | `CompletedDate` | `SoftDeletedDate` |
| --- | --- | --- |
| `Status = 'Submitted'` | `SubmitReportDateTime`, then `ModifyDate`, then `CreateDate` | null |
| `Status = 'CompletedNotSubmitted'` | `ModifyDate`, then `CreateDate` | null |
| `IsDeleted = 1` and any other status | null | `ModifyDate`, then `CreateDate` |

`ModifyDate` is the same as or later than the true date, so a backfilled report is kept a little
longer, which is the safe side. The 1243 decision names only `SubmitReportDateTime` for submitted
reports; the two fallbacks after it guard against a null that should not occur. The migration's
`Down` drops both columns. `ReportRetentionDatesMigrationTests` runs that `Down`, seeds one row per
case above, runs `Up`, and checks every value.

## Scaffolding note

Generate Report migrations from `DotNet/Report` with
`dotnet ef migrations add <Name> --project Report.csproj --startup-project Report.csproj`. Run from
the repository root, the tool takes `docker-compose.dcproj` as the startup project and fails. Read
the scaffold before keeping it: the committed model snapshot has drifted from the database in the
past, and the scaffold then includes operations the story did not ask for.
