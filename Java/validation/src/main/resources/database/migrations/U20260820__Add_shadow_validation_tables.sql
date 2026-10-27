-- Undo for V20260820__Add_shadow_validation_tables.sql
-- Drops foreign keys first, then tables in reverse dependency order.

if exists (select 1 from sys.foreign_keys where name = 'fk_legacy_shadow_finding_result')
    alter table legacy_shadow_finding drop constraint fk_legacy_shadow_finding_result;

drop table if exists legacy_shadow_finding;

drop table if exists legacy_shadow_result;

drop table if exists shadow_comparison_result;
