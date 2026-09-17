-- Drop old result tables (data will be lost per migration plan)
if exists (select 1 from sys.foreign_keys where name = 'fk_result_category_result_id')
begin
    alter table result_category drop constraint fk_result_category_result_id
end

if exists (select 1 from sys.foreign_keys where name = 'fk_result_category_category_id')
begin
    alter table result_category drop constraint fk_result_category_category_id
end

if exists (select 1 from sys.tables where name = 'result_category' and schema_id = schema_id('dbo'))
begin
    drop table result_category
end

if exists (select 1 from sys.tables where name = 'result' and schema_id = schema_id('dbo'))
begin
    drop table result
end

if exists (select 1 from sys.sequences where name = 'result_sequence' and schema_name(schema_id) = 'dbo')
begin
    drop sequence dbo.result_sequence
end

-- Create sequence for Result IDs
create sequence dbo.result_sequence
    as bigint
    start with 1
    increment by 100;

-- Recreate result table using sequence-based IDs (not IDENTITY)
create table result
(
    id          bigint not null primary key default (NEXT VALUE FOR dbo.result_sequence),
    expression  varchar(1000),
    code        varchar(255)    not null check (code in
                                                ('INVALID', 'STRUCTURE', 'REQUIRED', 'VALUE', 'INVARIANT', 'SECURITY',
                                                 'LOGIN', 'UNKNOWN', 'EXPIRED', 'FORBIDDEN', 'SUPPRESSED', 'PROCESSING',
                                                 'NOTSUPPORTED', 'DUPLICATE', 'MULTIPLEMATCHES', 'NOTFOUND', 'DELETED',
                                                 'TOOLONG', 'CODEINVALID', 'EXTENSION', 'TOOCOSTLY', 'BUSINESSRULE',
                                                 'CONFLICT', 'TRANSIENT', 'LOCKERROR', 'NOSTORE', 'EXCEPTION',
                                                 'TIMEOUT', 'INCOMPLETE', 'THROTTLED', 'INFORMATIONAL', 'NULL')),
    facility_id varchar(255)    not null,
    location    varchar(255),
    message     varchar(max)    not null,
    patient_id  varchar(255)    not null,
    report_id   varchar(255)    not null,
    severity    varchar(255)    not null check (severity in ('FATAL', 'ERROR', 'WARNING', 'INFORMATION', 'NULL'))
);

-- Recreate result_category join table
create table result_category
(
    result_id   bigint       not null,
    category_id varchar(255) not null
);

-- Indexes and constraints
if not exists (select 1 from sys.indexes where name = 'ix_result_facility_id' and object_id = object_id('result'))
    create index ix_result_facility_id
        on result (facility_id);

if not exists (select 1 from sys.indexes where name = 'ix_result_facility_id_report_id' and object_id = object_id('result'))
    create index ix_result_facility_id_report_id
        on result (facility_id, report_id);

if not exists (select 1 from sys.indexes where name = 'ix_result_facility_id_report_id_patient_id' and object_id = object_id('result'))
    create index ix_result_facility_id_report_id_patient_id
        on result (facility_id, report_id, patient_id);

if not exists (select 1 from sys.indexes where name = 'ix_result_category_result_id' and object_id = object_id('result_category'))
    create index ix_result_category_result_id
        on result_category (result_id);

if not exists (select 1 from sys.key_constraints where name = 'ix_result_category_result_id_category_id')
begin
    alter table result_category
        add constraint ix_result_category_result_id_category_id unique (result_id, category_id);
end;

-- Foreign keys
if not exists (select 1 from sys.foreign_keys where name = 'fk_result_category_category_id')
begin
    alter table result_category
        add constraint fk_result_category_category_id
            foreign key (category_id)
                references category;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_result_category_result_id')
begin
    alter table result_category
        add constraint fk_result_category_result_id
            foreign key (result_id)
                references result;
end;

-- Ensure existing sequence uses increment 100 to match Hibernate allocationSize
IF EXISTS (SELECT 1 FROM sys.sequences WHERE name = 'result_sequence' AND schema_name(schema_id) = 'dbo')
BEGIN
    ALTER SEQUENCE dbo.result_sequence
        INCREMENT BY 100;
END
ELSE
BEGIN
    CREATE SEQUENCE dbo.result_sequence
        AS bigint
        START WITH 1
        INCREMENT BY 100;
END
