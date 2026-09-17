-- Rolls back V20260623__Add_category_suppress_path_patterns.sql.

if exists (select 1 from sys.columns where name = 'suppress_path_patterns'
             and object_id = object_id('category'))
begin
    alter table category drop column suppress_path_patterns;
end;
