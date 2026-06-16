-- Rolls back V20260616__Add_category_strategy_and_scope.sql.
-- Drops the default constraint first because SQL Server won't drop a
-- column that has a referenced default constraint.

if exists (select 1 from sys.default_constraints where name = 'df_category_strategy')
begin
    alter table category drop constraint df_category_strategy;
end;

if exists (select 1 from sys.check_constraints
             where parent_object_id = object_id('category')
               and definition like '%strategy%')
begin
    declare @check_name nvarchar(256);
    select @check_name = name from sys.check_constraints
        where parent_object_id = object_id('category')
          and definition like '%strategy%';
    exec ('alter table category drop constraint ' + @check_name);
end;

if exists (select 1 from sys.columns where name = 'scope'
             and object_id = object_id('category'))
begin
    alter table category drop column scope;
end;

if exists (select 1 from sys.columns where name = 'strategy'
             and object_id = object_id('category'))
begin
    alter table category drop column strategy;
end;
