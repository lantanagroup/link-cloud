-- Undo for V20260804__Rubric_version_dry_run.sql

if exists (select 1 from sys.check_constraints where name = 'ck_rv_dry_run_status')
begin
    alter table rubric_version
        drop constraint ck_rv_dry_run_status;
end;

if exists (select 1
           from sys.columns
           where name = 'dry_run_completed_at'
             and object_id = object_id('rubric_version'))
begin
    alter table rubric_version
        drop column dry_run_completed_at;
end;

if exists (select 1
           from sys.columns
           where name = 'dry_run_status'
             and object_id = object_id('rubric_version'))
begin
    alter table rubric_version
        drop column dry_run_status;
end;
