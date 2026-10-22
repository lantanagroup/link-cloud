# Stored validation category counts

The validation service stores each report's count of findings per category as it validates the
report's patients. The pre-qualification screen can read those counts instead of downloading every
finding and counting them on each load, and the counts are kept after the report's findings are
deleted by retention. This page covers what is counted, how the counts are written and read, and
what retention has to do with them. The ticket is
[LEGLINK-1313](https://lantana.atlassian.net/browse/LEGLINK-1313), the Dev sub-task of
[LEGLINK-1312](https://lantana.atlassian.net/browse/LEGLINK-1312). The screen change is
[LEGLINK-1017](https://lantana.atlassian.net/browse/LEGLINK-1017), and the findings cleanup is
[LEGLINK-1215](https://lantana.atlassian.net/browse/LEGLINK-1215).

## The three tables

| Table | One row per | Kept |
| --- | --- | --- |
| `report_category_count` | report and category: `issues`, plus the category's `title`, `acceptable` and `submit` as they were when the count was last written | as long as the report |
| `patient_category_count` | report, patient and category: `issues` | until the report's findings are deleted |
| `report_category_count_status` | report: `last_updated` | as long as the report |

The report totals are what the endpoints read: about 50 rows per report, one per category that has
findings. The per-patient rows exist only so that a patient validated again replaces its earlier
counts instead of adding to them (see *Writing*). The status row does two jobs. Its presence means
the report's counts were written, which is how a report with no findings (status row, no totals)
differs from one validated before counts existed (no status row). It is also the row that is
locked while a patient's counts are applied.

There is no foreign key to `category`. A count must survive its category being deleted, which is
also why the title and flags are copied onto the total rather than joined.

The tables come from `V20261021__Add_category_counts.sql` in deployed environments, where every
statement is guarded because the pipeline re-applies every script on each deploy. The docker
profile creates them through Hibernate (`ddl-auto: update`).

Like the rest of validation's schema, the id columns are `varchar`. The SQL Server JDBC driver
sends string parameters as `nvarchar` by default. Compared with a `varchar` column under the SQL
collation, that makes SQL Server convert the column (`CONVERT_IMPLICIT`) and scan the whole index
instead of seeking. It made validation's `result` queries scan: one patient's findings read about
2,000 pages locally instead of 4. For these tables it would be worse, because the locked status
lookup would range-lock every report's row, so one report's counts would block all the others.

So validation's `application.yml` sets the driver's `sendStringParametersAsUnicode=false`, through
`spring.datasource.hikari.data-source-properties`. String parameters are then sent as `varchar` and
match the columns, and every lookup and the lock seek on their unique index. App Configuration
defines only the datasource URL and credentials for validation, so the setting applies in every
environment without provisioning. `DataSourceSettingsTest` fails if it is removed. A future
`nvarchar` column must be mapped with `@Nationalized`, so its parameters are still sent as
`nvarchar` and its non-Latin characters survive.

A local database built from an earlier version of this branch may have these columns as
`nvarchar`. That still seeks, so it needs no action; `ddl-auto: update` leaves existing column
types alone, so drop the three tables if you want the local schema to match.

## What is counted

`CategoryCountService.recordPatientCounts` counts one patient's findings, after categorization:

- A finding with categories is counted once in each of them, **whatever the category's `submit`
  flag**. Category ids are compared case-insensitively, as the database collation compares them,
  so two spellings of one id are one category.
- A finding with no category is counted under `uncategorized`, which has `submit = true`
  (`Category.UNCATEGORIZED`).

This is not the same set as the saved findings. `ReadyForValidationConsumer` saves only findings
with at least one `submit = true` category
([LEGLINK-653](https://lantana.atlassian.net/browse/LEGLINK-653)), so findings with no category, or
with only non-submitted categories, are counted but never saved. A drill-down into such a category
finds no findings to list.

The counts are fixed when each patient is validated. A later change to a category's rules, title
or flags does not move them. The screen's old live view, which re-categorized every finding with
the current rules on each load, did move.

## Writing a patient's counts

`ReadyForValidationConsumer.validate()` calls `recordPatientCounts` with every finding, saved or
not, before saving the submitted ones. The order matters on a retry: recording a patient's counts
again replaces them, but saving its findings again duplicates them. With the counts first, a
failure in either step leaves nothing for the retry to duplicate. Validation runs one patient at a
time and nothing marks a report as finished, so a report's totals fill in patient by patient and
are complete once its last patient has been validated.

A patient can be validated more than once for the same report: a Kafka redelivery, or the
`MeasureReportGenerated` null-key race sending `ReadyForValidation` twice. Adding each patient's
counts to the totals would count it twice. So each write replaces the patient's previous
contribution:

1. Create the report's status row if it doesn't exist, **in a transaction of its own**. Two
   patients of a new report can race to create it, and the loser's insert fails on the unique
   constraint. Catching that failure inside the counting transaction would leave that transaction
   marked rollback-only.
2. In a second transaction, lock the status row (`PESSIMISTIC_WRITE`; on SQL Server, `updlock,
   holdlock, rowlock`). Every patient of a report waits on the same row, so one report's totals are
   updated one patient at a time while other reports run in parallel. The totals can then be read
   and written through JPA without lost updates.
3. For each category the patient has now or had before, update the patient's row to the new count
   and add the difference to the report total. A row whose count reaches 0 is deleted.
4. Set the status row's `last_updated`.

Rows are updated in place, not deleted and re-inserted, because Hibernate flushes inserts before
deletes and a re-insert would hit the unique constraint before the delete ran.

A per-report count of patients was considered and dropped. A patient with no findings writes no
per-patient rows, so a repeat of that patient can't be detected and would be counted twice.

## Reading the counts

**Validation:**
`GET /api/validation/facilities/{facilityId}/reports/{reportId}/category-counts?submit=`

- `200`: `{ facilityId, reportId, lastUpdated, categories: [{ categoryId, title, acceptable, submit,
  issues }] }`, most issues first, then by category id. `lastUpdated` is ISO-8601.
- `submit=true` or `submit=false` returns only the categories with that flag; without it, all of
  them. `submit=true` gives the submitted categories plus `uncategorized`.
- `200` with an empty list: the counts were written but no category matches, including a report
  whose patients had no findings.
- `404` Problem: the report has no stored counts.

**Admin BFF:**
`GET /api/aggregate/reports/{reportScheduleId}/validation-category-counts?submit=`

The BFF looks up the report schedule's facility in Report, calls the validation endpoint, and adds
each category's current `guidance` and `severity` from validation's category list, matched by id
case-insensitively. A category that no longer exists, and `uncategorized`, keep their stored title
and get no guidance. If the category list can't be read, the counts are still returned without
guidance and a warning is logged. The handler returns `400` for an id that isn't a GUID, and `404`
when the report schedule doesn't exist or has no stored counts, with the Problem detail saying
which.

The older `GET /api/validation/result/{facilityId}/{reportId}/category` still counts the saved
findings live, loading every one of them. Nothing in the Admin UI calls it. The E2E check below
uses it as the independent count.

## Retention

LEGLINK-1215 deletes a report's findings. It must delete the report's `patient_category_count` rows
in the same step: they are only needed while a patient can still be validated again, which ends
with its findings. It must keep `report_category_count` and `report_category_count_status`. Those
are what the screen shows after the findings are gone.

## Testing

- `CategoryCountJpaTest` and `CategoryCountServiceJpaTest` run against H2 in MSSQL mode. The service
  test runs with real transactions, including 16 patients of one report written concurrently, so
  the status-row lock and the first-insert race behave as they do in the consumer.
- `GetReportValidationCategoryCountsTests` covers the BFF handler.
- `ValidationResultsValidator` checks every Automation run's counts against its saved findings. A
  submitted category's stored total must equal its live count, since all its findings are saved. A
  non-submitted category's total must be at least its live count, since it also includes findings
  that weren't saved. Uncategorized findings are never saved, so they are skipped. Missing counts
  fail only when the report has saved findings. Above 100,000 saved findings
  (`CategoryCountComparisonLimit`) the live comparison is skipped and logged, because validation
  loads every finding of the report to count them live.
