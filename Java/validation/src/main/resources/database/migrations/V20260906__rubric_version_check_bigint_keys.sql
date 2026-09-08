-- Extends the bigint-surrogate-key conversion from V20260904 (rubric_result/rubric_finding)
-- to rubric_version.rubric_version_id and rubric_check.check_id, which were still random
-- uniqueidentifier PKs. rubric.rubric_id is a human-chosen business key (e.g. "piqi.core"),
-- not a surrogate id, and is intentionally left as varchar/unconverted.
--
-- Because rubric_result.rubric_version_id and rubric_finding.check_id are FK columns typed
-- against the columns being converted, this migration has to cascade through all four
-- tables (rubric_version, rubric_check, rubric_result, rubric_finding), not just the two
-- being changed directly. Data will be lost per migration plan (dev/pre-release schema
-- churn, same convention as V20260812__result_sequence_recreate_table.sql and V20260904).

set quoted_identifier on;

-- ---------------------------------------------------------------------------
-- Drop dependents first: FKs, then tables, child-to-parent.
-- ---------------------------------------------------------------------------

if exists (select 1 from sys.foreign_keys where name = 'fk_finding_result')
    alter table rubric_finding drop constraint fk_finding_result;

if exists (select 1 from sys.foreign_keys where name = 'fk_finding_check')
    alter table rubric_finding drop constraint fk_finding_check;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric')
    alter table rubric_result drop constraint fk_result_rubric;

if exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric_version')
    alter table rubric_result drop constraint fk_result_rubric_version;

if exists (select 1 from sys.foreign_keys where name = 'fk_check_rubric_version')
    alter table rubric_check drop constraint fk_check_rubric_version;

if exists (select 1 from sys.foreign_keys where name = 'fk_rubric_version_rubric')
    alter table rubric_version drop constraint fk_rubric_version_rubric;

if exists (select 1 from sys.tables where name = 'rubric_finding' and schema_id = schema_id('dbo'))
    drop table rubric_finding;

if exists (select 1 from sys.tables where name = 'rubric_result' and schema_id = schema_id('dbo'))
    drop table rubric_result;

if exists (select 1 from sys.tables where name = 'rubric_check' and schema_id = schema_id('dbo'))
    drop table rubric_check;

if exists (select 1 from sys.tables where name = 'rubric_version' and schema_id = schema_id('dbo'))
    drop table rubric_version;

-- ---------------------------------------------------------------------------
-- Sequences. Increment matches Hibernate's allocationSize (50). rubric_result_sequence and
-- rubric_finding_sequence already exist from V20260904 but are recreated here since their
-- tables are being dropped and rebuilt in this same script.
-- ---------------------------------------------------------------------------

if exists (select 1 from sys.sequences where name = 'rubric_version_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_version_sequence;
create sequence dbo.rubric_version_sequence as bigint start with 1 increment by 50;

if exists (select 1 from sys.sequences where name = 'rubric_check_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_check_sequence;
create sequence dbo.rubric_check_sequence as bigint start with 1 increment by 50;

if exists (select 1 from sys.sequences where name = 'rubric_result_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_result_sequence;
create sequence dbo.rubric_result_sequence as bigint start with 1 increment by 50;

if exists (select 1 from sys.sequences where name = 'rubric_finding_sequence' and schema_name(schema_id) = 'dbo')
    drop sequence dbo.rubric_finding_sequence;
create sequence dbo.rubric_finding_sequence as bigint start with 1 increment by 50;

-- ---------------------------------------------------------------------------
-- Tables, parent-to-child.
-- ---------------------------------------------------------------------------

create table rubric_version
(
    rubric_version_id       bigint            not null primary key default (NEXT VALUE FOR dbo.rubric_version_sequence),
    rubric_id               varchar(128)      not null,
    semver                  varchar(32)       not null,
    status                  varchar(16)       not null check (status in ('DRAFT', 'PUBLISHED', 'RETIRED')),
    published_at            datetimeoffset(6),
    published_by            varchar(128),
    retired_at              datetimeoffset(6),
    retired_by              varchar(128),
    dry_run_completed_at    datetimeoffset(6),
    dry_run_status          varchar(32) check (dry_run_status in
                                               ('ACCEPTABLE', 'ACCEPTABLE_WITH_WARNINGS', 'UNACCEPTABLE',
                                                'INCONCLUSIVE')),
    checksum                varchar(64)       not null,
    definition_json         varchar(max),
    dimensions_json         varchar(max),
    applicable_context_json varchar(max),
    scoring_policy_json     varchar(max),
    created_at              datetimeoffset(6) not null,
    created_by              varchar(128)
);

create table rubric_check
(
    check_id          bigint           not null primary key default (NEXT VALUE FOR dbo.rubric_check_sequence),
    rubric_version_id bigint           not null,
    check_local_id    varchar(128)     not null,
    type              varchar(32)      not null check (type in
                                                       ('FHIR_CONFORMANCE', 'TERMINOLOGY', 'FHIRPATH', 'VALUESET',
                                                        'PLAUSIBILITY', 'COMPLETENESS', 'CURRENCY', 'CUSTOM')),
    dimension         varchar(32)      not null check (dimension in
                                                       ('CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY',
                                                        'CURRENCY')),
    parameters_json   varchar(max),
    severity_override varchar(16) check (severity_override in ('ERROR', 'WARNING', 'INFORMATION')),
    ordinal           int,
    enabled           bit              not null,
    deleted           bit              not null default 0
);

create table rubric_result
(
    result_id                 bigint            not null primary key default (NEXT VALUE FOR dbo.rubric_result_sequence),
    request_id                uniqueidentifier  not null,
    rubric_id                 varchar(128)      not null,
    rubric_version_id         bigint            not null,
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
    finding_id bigint       not null primary key default (NEXT VALUE FOR dbo.rubric_finding_sequence),
    result_id  bigint       not null,
    check_id   bigint       not null,
    dimension  varchar(32)  not null check (dimension in
                                            ('CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY',
                                             'CURRENCY')),
    severity   varchar(16)  not null check (severity in ('ERROR', 'WARNING', 'INFORMATION')),
    code       varchar(128) not null,
    message    varchar(max) not null,
    location   varchar(512),
    expression varchar(max)
);

-- ---------------------------------------------------------------------------
-- Unique constraints, indexes (excludes ix_result_rubric, ix_result_facility_report,
-- ix_result_status, ix_result_workflow, ix_finding_result - dropped as unused/redundant)
-- ---------------------------------------------------------------------------

alter table rubric_version
    add constraint uq_rv_rubric_semver unique (rubric_id, semver);

-- (rubric_version_id, check_local_id) is only unique among live rows, a replacement check
-- from a draft re-registration can reuse a soft-deleted predecessor's local id
create unique nonclustered index uq_check_rv_local_active
    on rubric_check (rubric_version_id, check_local_id)
    where deleted = 0;

create index ix_check_rv_ordinal on rubric_check (rubric_version_id, ordinal);

alter table rubric_result
    add constraint uq_result_request unique (request_id);

create index ix_finding_check on rubric_finding (check_id);

create index ix_finding_severity on rubric_finding (result_id, severity);

-- ---------------------------------------------------------------------------
-- Foreign keys
-- ---------------------------------------------------------------------------

alter table rubric_version
    add constraint fk_rubric_version_rubric
        foreign key (rubric_id) references rubric;

alter table rubric_check
    add constraint fk_check_rubric_version
        foreign key (rubric_version_id) references rubric_version;

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

-- ---------------------------------------------------------------------------
-- Re-apply compression + off-row LOB storage (V20260905) since these are freshly
-- recreated table objects.
-- ---------------------------------------------------------------------------

exec sp_tableoption 'dbo.rubric_version', 'large value types out of row', 1;
alter index all on rubric_version rebuild with (data_compression = page);

exec sp_tableoption 'dbo.rubric_check', 'large value types out of row', 1;
alter index all on rubric_check rebuild with (data_compression = page);

exec sp_tableoption 'dbo.rubric_result', 'large value types out of row', 1;
alter index all on rubric_result rebuild with (data_compression = page);

exec sp_tableoption 'dbo.rubric_finding', 'large value types out of row', 1;
alter index all on rubric_finding rebuild with (data_compression = page);
