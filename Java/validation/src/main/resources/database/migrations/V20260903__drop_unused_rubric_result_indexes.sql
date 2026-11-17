-- Drops secondary indexes on rubric_result that are not exercised by any application
-- query today (RubricResultRepository only exposes findByRequestId, which is served
-- by the uq_result_request unique index, unaffected by this script).
--
-- Each of these is checked separately: if a query pattern against one of these columns
-- shows up later (app code or external reporting), re-add just that index rather than
-- restoring all four.

if exists (select 1 from sys.indexes where name = 'ix_result_rubric' and object_id = object_id('rubric_result'))
    drop index ix_result_rubric on rubric_result;

if exists (select 1 from sys.indexes where name = 'ix_result_facility_report' and object_id = object_id('rubric_result'))
    drop index ix_result_facility_report on rubric_result;

if exists (select 1 from sys.indexes where name = 'ix_result_status' and object_id = object_id('rubric_result'))
    drop index ix_result_status on rubric_result;

if exists (select 1 from sys.indexes where name = 'ix_result_workflow' and object_id = object_id('rubric_result'))
    drop index ix_result_workflow on rubric_result;
