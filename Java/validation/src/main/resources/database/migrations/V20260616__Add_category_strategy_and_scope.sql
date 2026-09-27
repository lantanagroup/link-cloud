-- Adds the strategy and scope columns introduced for the categories.json
-- per-rule handling strategy (Phase 1). See VALIDATION-CATEGORIES-DESIGN.md.
--
-- strategy: enum (SKIP/SUPPRESS/LABEL); existing rows backfill to 'LABEL' so
--           the change is behaviour-preserving until rules are deliberately
--           promoted.
-- scope:    JSON; nullable. Only populated for SKIP rules.

if not exists (select 1 from sys.columns where name = 'strategy'
                 and object_id = object_id('category'))
begin
    alter table category
        add strategy varchar(50)
            constraint df_category_strategy default 'LABEL' not null
            check (strategy in ('SKIP', 'SUPPRESS', 'LABEL'));
end;

if not exists (select 1 from sys.columns where name = 'scope'
                 and object_id = object_id('category'))
begin
    alter table category add scope varchar(max) null;
end;
