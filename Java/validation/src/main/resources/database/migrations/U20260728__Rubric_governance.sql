-- Undo for V20260728__Rubric_governance.sql
-- Drops foreign keys first, then tables in reverse dependency order.

if exists (select 1 from sys.foreign_keys where name = 'fk_finding_result')
    alter table rubric_finding drop constraint fk_finding_result;

if exists (select 1 from sys.foreign_keys where name = 'fk_finding_check')
    alter table rubric_finding drop constraint fk_finding_check;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric')
    alter table rubric_result drop constraint fk_result_rubric;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric_version')
    alter table rubric_result drop constraint fk_result_rubric_version;

if exists (select 1 from sys.foreign_keys where name = 'fk_lifecycle_event_rubric')
    alter table rubric_lifecycle_event drop constraint fk_lifecycle_event_rubric;

if exists (select 1 from sys.foreign_keys where name = 'fk_check_rubric_version')
    alter table rubric_check drop constraint fk_check_rubric_version;

if exists (select 1 from sys.foreign_keys where name = 'fk_rubric_version_rubric')
    alter table rubric_version drop constraint fk_rubric_version_rubric;

drop table if exists rubric_finding;

drop table if exists rubric_result;

drop table if exists rubric_lifecycle_event;

drop table if exists rubric_check;

drop table if exists rubric_version;

drop table if exists rubric;

if exists (select 1 from sys.sequences where name = 'rubric_finding_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_finding_sequence;

if exists (select 1 from sys.sequences where name = 'rubric_result_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_result_sequence;

if exists (select 1 from sys.sequences where name = 'rubric_check_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_check_sequence;

if exists (select 1 from sys.sequences where name = 'rubric_version_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_version_sequence;
