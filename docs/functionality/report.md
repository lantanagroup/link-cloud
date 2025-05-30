[← Back Home](../README.md)

# Reports

Different types of report generation methods:

* Scheduled Reports - Scheduling system used to automatically create reports (aka: ScheduledReport events) based on schedule routine (ie. a CRON specification)
* Adhoc Report - Create a ScheduledReport event manually for a one-time configuration of tenant, measures and reporting period (dates). You may optionally specify the list of patients for the report; if not specified, it will ask the census service for the patients of interest during the reporting period dates.
* Regenerate Report - Provide a previously created report ID to re-generate the report, bypassing data acquisition and normalization, and skipping straight to _evaluation_. This is useful in cases where the measure has changed and a report needs to be re-evaluated against the new measure version.