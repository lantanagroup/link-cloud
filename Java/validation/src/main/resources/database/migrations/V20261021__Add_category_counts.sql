-- LEGLINK-1313: per-category validation counts that outlive a report's findings. The pipeline re-applies every V*.sql
-- on each deploy, so every statement is guarded. There is no foreign key to category: a count must survive the
-- category being deleted.
if not exists (select 1 from sys.tables where name = 'report_category_count' and schema_id = schema_id('dbo'))
begin
    create table report_category_count
    (
        id          bigint identity not null,
        facility_id varchar(255)    not null,
        report_id   varchar(255)    not null,
        category_id varchar(255)    not null,
        title       varchar(255)    not null,
        acceptable  bit             not null,
        submit      bit             not null,
        issues      bigint          not null,
        primary key (id)
    );
end;

if not exists (select 1 from sys.tables where name = 'patient_category_count' and schema_id = schema_id('dbo'))
begin
    create table patient_category_count
    (
        id          bigint identity not null,
        facility_id varchar(255)    not null,
        report_id   varchar(255)    not null,
        patient_id  varchar(255)    not null,
        category_id varchar(255)    not null,
        issues      bigint          not null,
        primary key (id)
    );
end;

if not exists (select 1 from sys.tables where name = 'report_category_count_status' and schema_id = schema_id('dbo'))
begin
    create table report_category_count_status
    (
        id           bigint identity   not null,
        facility_id  varchar(255)      not null,
        report_id    varchar(255)      not null,
        last_updated datetimeoffset(6) not null,
        primary key (id)
    );
end;

-- Each unique constraint also serves the lookups, which all filter on a leading prefix of its columns.
if not exists (select 1
               from sys.key_constraints
               where name = 'uq_report_category_count_facility_id_report_id_category_id')
begin
    alter table report_category_count
        add constraint uq_report_category_count_facility_id_report_id_category_id
            unique (facility_id, report_id, category_id);
end;

if not exists (select 1
               from sys.key_constraints
               where name = 'uq_patient_category_count_facility_id_report_id_patient_id_category_id')
begin
    alter table patient_category_count
        add constraint uq_patient_category_count_facility_id_report_id_patient_id_category_id
            unique (facility_id, report_id, patient_id, category_id);
end;

if not exists (select 1
               from sys.key_constraints
               where name = 'uq_report_category_count_status_facility_id_report_id')
begin
    alter table report_category_count_status
        add constraint uq_report_category_count_status_facility_id_report_id
            unique (facility_id, report_id);
end;
