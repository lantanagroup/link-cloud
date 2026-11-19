-- The severity each rubric check emitted, before any category override. rubric_finding.severity holds the severity
-- after it, which is what the result's status is decided on, while the result's counts are of the severities before
-- it; storing both lets a reader reconcile them and see which findings a category moved. Nullable: rows written
-- before this column existed don't have it. The pipeline re-applies every V*.sql on each deploy, so this is guarded.
if not exists (select 1 from sys.columns where object_id = object_id('rubric_finding') and name = 'original_severity')
begin
    alter table rubric_finding add original_severity varchar(16) null
        constraint ck_rubric_finding_original_severity check (original_severity in ('ERROR', 'WARNING', 'INFORMATION'));
end;
