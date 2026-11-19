-- Lets rubric_lifecycle_event.action hold 'DELETED', recorded when a draft version that was never published is deleted.
--
-- The column's check constraint was created without a name, so it has whatever name the database gave it. Every check
-- constraint on that column that does not allow 'DELETED' is dropped, and one with a known name is added in its place.
-- The pipeline re-applies every V*.sql on each deploy, so each step is guarded and a second run changes nothing.
if object_id('rubric_lifecycle_event') is not null
begin
    declare @drop_rubric_lifecycle_action_checks nvarchar(max) = N'';

    select @drop_rubric_lifecycle_action_checks = @drop_rubric_lifecycle_action_checks
        + N'alter table rubric_lifecycle_event drop constraint ' + quotename(cc.name) + N';'
    from sys.check_constraints cc
             join sys.columns col on col.object_id = cc.parent_object_id and col.column_id = cc.parent_column_id
    where cc.parent_object_id = object_id('rubric_lifecycle_event')
      and col.name = 'action'
      and cc.definition not like '%DELETED%';

    if len(@drop_rubric_lifecycle_action_checks) > 0
        exec sp_executesql @drop_rubric_lifecycle_action_checks;

    if not exists (select 1
                   from sys.check_constraints cc
                            join sys.columns col on col.object_id = cc.parent_object_id and col.column_id = cc.parent_column_id
                   where cc.parent_object_id = object_id('rubric_lifecycle_event')
                     and col.name = 'action')
        alter table rubric_lifecycle_event
            add constraint ck_rubric_lifecycle_event_action
                check (action in ('REGISTERED', 'PUBLISHED', 'RETIRED', 'DELETED'));
end;
