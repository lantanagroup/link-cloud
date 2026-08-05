-- Dry-run gate columns on rubric_version: record whether a dry run was completed for the
-- rubric+semver and its outcome. Read at publish time when
-- link.rubric.dry-run.required-for-publish is enabled; written by a later feature.

if not exists (select 1
               from sys.columns
               where name = 'dry_run_completed_at'
                 and object_id = object_id('rubric_version'))
begin
    alter table rubric_version
        add dry_run_completed_at datetimeoffset(6);
end;

if not exists (select 1
               from sys.columns
               where name = 'dry_run_status'
                 and object_id = object_id('rubric_version'))
begin
    alter table rubric_version
        add dry_run_status varchar(32);
end;

-- Named (unlike the inline checks created with the tables) because alter table add
-- requires a name and the undo script references it. NULL passes the check, so rows
-- without a dry run are unaffected.
if not exists (select 1 from sys.check_constraints where name = 'ck_rv_dry_run_status')
begin
    alter table rubric_version
        add constraint ck_rv_dry_run_status check (dry_run_status in
            ('ACCEPTABLE', 'ACCEPTABLE_WITH_WARNINGS', 'UNACCEPTABLE', 'INCONCLUSIVE'));
end;
