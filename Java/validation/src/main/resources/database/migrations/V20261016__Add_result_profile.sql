-- LEGLINK-1172: the profile that produced each validation finding. Nullable: findings from base-FHIR and
-- best-practice checks belong to no profile. The pipeline re-applies every V*.sql on each deploy, so this is guarded.
if not exists (select 1 from sys.columns where object_id = object_id('result') and name = 'profile')
begin
    alter table result add profile varchar(1000) null;
end;
