-- Reverts V20261108__Add_rubric_finding_original_severity.sql.
if exists (select 1 from sys.check_constraints where name = 'ck_rubric_finding_original_severity')
begin
    alter table rubric_finding drop constraint ck_rubric_finding_original_severity;
end;

if exists (select 1 from sys.columns where object_id = object_id('rubric_finding') and name = 'original_severity')
begin
    alter table rubric_finding drop column original_severity;
end;
