-- Reverts V20261016__Add_result_profile.sql.
if exists (select 1 from sys.columns where object_id = object_id('result') and name = 'profile')
begin
    alter table result drop column profile;
end;
