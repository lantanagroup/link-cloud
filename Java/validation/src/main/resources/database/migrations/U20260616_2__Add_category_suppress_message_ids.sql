-- Rolls back V20260616_2__Add_category_suppress_message_ids.sql.

if exists (select 1 from sys.columns where name = 'suppress_message_ids'
             and object_id = object_id('category'))
begin
    alter table category drop column suppress_message_ids;
end;
