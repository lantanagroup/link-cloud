-- Switches rubric_result.result_id and rubric_finding.finding_id from a random uniqueidentifier
-- (random-order clustered PK -> page splits/fragmentation on every insert, see perf discussion)
-- to a bigint surrogate key backed by an ascending sequence, matching the pattern already used
-- by dbo.result_sequence for the legacy `result` table. request_id (client-supplied idempotency
-- key) and check_id (FK to rubric_check, still uniqueidentifier) are unaffected.
--
-- Data will be lost per migration plan (dev/pre-release schema churn, same convention as
-- V20260812__result_sequence_recreate_table.sql).

set quoted_identifier on;

-- Drop dependents first: rubric_finding FKs to rubric_result, then the tables themselves.
if exists (select 1 from sys.foreign_keys where name = 'fk_finding_result')
begin
    alter table rubric_finding drop constraint fk_finding_result;
end;

if exists (select 1 from sys.foreign_keys where name = 'fk_finding_check')
begin
    alter table rubric_finding drop constraint fk_finding_check;
end;

if exists (select 1 from sys.tables where name = 'rubric_finding' and schema_id = schema_id('dbo'))
begin
    drop table rubric_finding;
end;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric')
begin
    alter table rubric_result drop constraint fk_result_rubric;
end;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric_version')
begin
    alter table rubric_result drop constraint fk_result_rubric_version;
end;

if exists (select 1 from sys.tables where name = 'rubric_result' and schema_id = schema_id('dbo'))
begin
    drop table rubric_result;
end;

-- Sequences backing the new surrogate keys. Increment matches Hibernate's allocationSize (50)
-- so each app-side sequence fetch consumes exactly one DB round trip per batch.
if exists (select 1 from sys.sequences where name = 'rubric_result_sequence' and schema_name(schema_id) = 'dbo')
begin
    drop sequence dbo.rubric_result_sequence;
end;

create sequence dbo.rubric_result_sequence
    as bigint
    start with 1
    increment by 50;

if exists (select 1 from sys.sequences where name = 'rubric_finding_sequence' and schema_name(schema_id) = 'dbo')
begin
    drop sequence dbo.rubric_finding_sequence;
end;

create sequence dbo.rubric_finding_sequence
    as bigint
    start with 1
    increment by 50;

-- ---------------------------------------------------------------------------
-- Tables (bigint PKs, ascending clustered index -> inserts append instead of splitting)
-- ---------------------------------------------------------------------------

create table rubric_result
(
    result_id                 bigint            not null primary key default (NEXT VALUE FOR dbo.rubric_result_sequence),
    request_id                uniqueidentifier  not null,
    rubric_id                 varchar(128)      not null,
    rubric_version_id         uniqueidentifier  not null,
    status                    varchar(32)       not null check (status in
                                                                ('ACCEPTABLE', 'ACCEPTABLE_WITH_WARNINGS',
                                                                 'UNACCEPTABLE', 'INCONCLUSIVE')),
    score_json                varchar(max),
    error_count               int               not null,
    warning_count             int               not null,
    information_count         int               not null,
    supporting_artifacts_json varchar(max),
    correlation_id            varchar(128),
    requestor                 varchar(128),
    payload_ref               varchar(512),
    facility_id               varchar(128),
    patient_id                varchar(128),
    report_id                 varchar(128),
    workflow_tag              varchar(128),
    stage                     varchar(64),
    requested_at              datetimeoffset(6) not null,
    completed_at              datetimeoffset(6) not null,
    duration_ms               bigint            not null
);

create table rubric_finding
(
    finding_id bigint           not null primary key default (NEXT VALUE FOR dbo.rubric_finding_sequence),
    result_id  bigint           not null,
    check_id   uniqueidentifier not null,
    dimension  varchar(32)      not null check (dimension in
                                                ('CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY',
                                                 'CURRENCY')),
    severity   varchar(16)      not null check (severity in ('ERROR', 'WARNING', 'INFORMATION')),
    code       varchar(128)     not null,
    message    varchar(max)     not null,
    location   varchar(512),
    expression varchar(max)
);

-- ---------------------------------------------------------------------------
-- Unique constraints, indexes (deliberately excludes ix_result_rubric, ix_result_facility_report,
-- ix_result_status, ix_result_workflow, ix_finding_result - dropped as unused/redundant, see
-- V20260903__drop_unused_rubric_result_indexes.sql)
-- ---------------------------------------------------------------------------

alter table rubric_result
    add constraint uq_result_request unique (request_id);

create index ix_finding_check on rubric_finding (check_id);

create index ix_finding_severity on rubric_finding (result_id, severity);

-- ---------------------------------------------------------------------------
-- Foreign keys
-- ---------------------------------------------------------------------------

alter table rubric_result
    add constraint fk_result_rubric
        foreign key (rubric_id) references rubric;

alter table rubric_result
    add constraint fk_result_rubric_version
        foreign key (rubric_version_id) references rubric_version;

alter table rubric_finding
    add constraint fk_finding_result
        foreign key (result_id) references rubric_result;

alter table rubric_finding
    add constraint fk_finding_check
        foreign key (check_id) references rubric_check;
