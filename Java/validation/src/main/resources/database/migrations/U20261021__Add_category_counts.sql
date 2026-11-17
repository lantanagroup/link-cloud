-- Reverts V20261021__Add_category_counts.sql.
if exists (select 1 from sys.tables where name = 'patient_category_count' and schema_id = schema_id('dbo'))
begin
    drop table patient_category_count;
end;

if exists (select 1 from sys.tables where name = 'report_category_count' and schema_id = schema_id('dbo'))
begin
    drop table report_category_count;
end;

if exists (select 1 from sys.tables where name = 'report_category_count_status' and schema_id = schema_id('dbo'))
begin
    drop table report_category_count_status;
end;
