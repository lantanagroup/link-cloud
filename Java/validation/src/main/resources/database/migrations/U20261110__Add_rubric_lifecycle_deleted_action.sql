-- Reverts V20261110__Add_rubric_lifecycle_deleted_action.sql.
--
-- The earlier version of the service cannot read an event whose action is 'DELETED', so those events are removed
-- before the constraint that forbids them is put back. They record deletions of drafts that were never published;
-- nothing else refers to them.
if object_id('rubric_lifecycle_event') is not null
begin
    if exists (select 1 from sys.check_constraints where name = 'ck_rubric_lifecycle_event_action')
        alter table rubric_lifecycle_event drop constraint ck_rubric_lifecycle_event_action;

    delete from rubric_lifecycle_event where action = 'DELETED';

    if not exists (select 1
                   from sys.check_constraints cc
                            join sys.columns col on col.object_id = cc.parent_object_id and col.column_id = cc.parent_column_id
                   where cc.parent_object_id = object_id('rubric_lifecycle_event')
                     and col.name = 'action')
        alter table rubric_lifecycle_event
            add constraint ck_rubric_lifecycle_event_action
                check (action in ('REGISTERED', 'PUBLISHED', 'RETIRED'));
end;
