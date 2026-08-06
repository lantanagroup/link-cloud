-- Rubric governance schema: rubric registry (rubric, rubric_version, rubric_check),
-- lifecycle audit (rubric_lifecycle_event), facility-level overrides (facility_override),
-- and evaluation results (rubric_result, rubric_finding).

-- needed for the filtered unique index below, sqlcmd defaults this to OFF
set quoted_identifier on;

if not exists (select 1 from sys.tables where name = 'rubric' and schema_id = schema_id('dbo'))
begin
    create table rubric
    (
        rubric_id  varchar(128)      not null,
        title      varchar(256)      not null,
        owner      varchar(128),
        created_at datetimeoffset(6) not null,
        updated_at datetimeoffset(6) not null,
        primary key (rubric_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'rubric_version' and schema_id = schema_id('dbo'))
begin
    create table rubric_version
    (
        rubric_version_id       uniqueidentifier  not null,
        rubric_id               varchar(128)      not null,
        semver                  varchar(32)       not null,
        status                  varchar(16)       not null check (status in ('DRAFT', 'PUBLISHED', 'RETIRED')),
        published_at            datetimeoffset(6),
        published_by            varchar(128),
        retired_at              datetimeoffset(6),
        retired_by              varchar(128),
        -- set when a $dry-run completes, publish checks these when the dry-run gate is on
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
        created_by              varchar(128),
        primary key (rubric_version_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'rubric_check' and schema_id = schema_id('dbo'))
begin
    create table rubric_check
    (
        check_id          uniqueidentifier not null,
        rubric_version_id uniqueidentifier not null,
        check_local_id    varchar(64)      not null,
        type              varchar(32)      not null check (type in
                                                           ('FHIR_CONFORMANCE', 'TERMINOLOGY', 'FHIRPATH', 'VALUESET',
                                                            'PLAUSIBILITY', 'COMPLETENESS', 'CURRENCY', 'CUSTOM')),
        dimension         varchar(32)      not null check (dimension in
                                                           ('CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY',
                                                            'CURRENCY')),
        parameters_json   varchar(max),
        severity_override varchar(16) check (severity_override in ('ERROR', 'WARNING', 'INFORMATION')),
        -- nullable, checks without an ordinal run first (NULL sorts first)
        ordinal           int,
        enabled           bit              not null,
        -- soft delete, set when a draft re-registration replaces the version's checks
        deleted           bit              not null default 0,
        primary key (check_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'facility_override' and schema_id = schema_id('dbo'))
begin
    create table facility_override
    (
        override_id             uniqueidentifier  not null,
        facility_id             varchar(128)      not null,
        rubric_id               varchar(128)      not null,
        rubric_version_id       uniqueidentifier,
        disabled_check_ids_json varchar(max),
        severity_overrides_json varchar(max),
        context_vars_json       varchar(max),
        effective_from          datetimeoffset(6) not null,
        effective_to            datetimeoffset(6),
        created_at              datetimeoffset(6) not null,
        created_by              varchar(128)      not null,
        primary key (override_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'rubric_result' and schema_id = schema_id('dbo'))
begin
    create table rubric_result
    (
        result_id                 uniqueidentifier  not null,
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
        facility_override_id      uniqueidentifier,
        requested_at              datetimeoffset(6) not null,
        completed_at              datetimeoffset(6) not null,
        duration_ms               bigint            not null,
        primary key (result_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'rubric_finding' and schema_id = schema_id('dbo'))
begin
    create table rubric_finding
    (
        finding_id uniqueidentifier not null,
        result_id  uniqueidentifier not null,
        check_id   uniqueidentifier not null,
        dimension  varchar(32)      not null check (dimension in
                                                    ('CONFORMANCE', 'TERMINOLOGY', 'COMPLETENESS', 'PLAUSIBILITY',
                                                     'CURRENCY')),
        severity   varchar(16)      not null check (severity in ('ERROR', 'WARNING', 'INFORMATION')),
        code       varchar(128)     not null,
        message    varchar(max)     not null,
        location   varchar(512),
        expression varchar(max),
        primary key (finding_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'rubric_lifecycle_event' and schema_id = schema_id('dbo'))
begin
    create table rubric_lifecycle_event
    (
        event_id    uniqueidentifier  not null,
        rubric_id   varchar(128)      not null,
        semver      varchar(32)       not null,
        action      varchar(16)       not null check (action in ('REGISTERED', 'PUBLISHED', 'RETIRED')),
        actor       varchar(128),
        checksum    varchar(64),
        occurred_at datetimeoffset(6) not null,
        primary key (event_id)
    );
end;

-- ---------------------------------------------------------------------------
-- Unique constraints
-- ---------------------------------------------------------------------------

if not exists (select 1 from sys.key_constraints where name = 'uq_rv_rubric_semver')
begin
    alter table rubric_version
        add constraint uq_rv_rubric_semver unique (rubric_id, semver);
end;

-- (rubric_version_id, check_local_id) is only unique among live rows, a replacement check
-- from a draft re-registration can reuse a soft-deleted predecessor's local id
if not exists (select 1 from sys.indexes where name = 'uq_check_rv_local_active'
                 and object_id = object_id('rubric_check'))
begin
    create unique nonclustered index uq_check_rv_local_active
        on rubric_check (rubric_version_id, check_local_id)
        where deleted = 0;
end;

if not exists (select 1 from sys.key_constraints where name = 'uq_fo_facility_rubric_effective')
begin
    alter table facility_override
        add constraint uq_fo_facility_rubric_effective unique (facility_id, rubric_id, effective_from);
end;

if not exists (select 1 from sys.key_constraints where name = 'uq_result_request')
begin
    alter table rubric_result
        add constraint uq_result_request unique (request_id);
end;

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------

if not exists (select 1 from sys.indexes where name = 'ix_check_rv_ordinal' and object_id = object_id('rubric_check'))
    create index ix_check_rv_ordinal on rubric_check (rubric_version_id, ordinal);

if not exists (select 1 from sys.indexes where name = 'ix_fo_lookup' and object_id = object_id('facility_override'))
    create index ix_fo_lookup on facility_override (facility_id, rubric_id, effective_from);

if not exists (select 1 from sys.indexes where name = 'ix_result_rubric' and object_id = object_id('rubric_result'))
    create index ix_result_rubric on rubric_result (rubric_id, completed_at);

if not exists (select 1 from sys.indexes where name = 'ix_result_facility_report' and object_id = object_id('rubric_result'))
    create index ix_result_facility_report on rubric_result (facility_id, report_id);

if not exists (select 1 from sys.indexes where name = 'ix_result_status' and object_id = object_id('rubric_result'))
    create index ix_result_status on rubric_result (status, completed_at);

if not exists (select 1 from sys.indexes where name = 'ix_result_workflow' and object_id = object_id('rubric_result'))
    create index ix_result_workflow on rubric_result (workflow_tag, completed_at);

if not exists (select 1 from sys.indexes where name = 'ix_finding_result' and object_id = object_id('rubric_finding'))
    create index ix_finding_result on rubric_finding (result_id);

if not exists (select 1 from sys.indexes where name = 'ix_finding_check' and object_id = object_id('rubric_finding'))
    create index ix_finding_check on rubric_finding (check_id);

if not exists (select 1 from sys.indexes where name = 'ix_finding_severity' and object_id = object_id('rubric_finding'))
    create index ix_finding_severity on rubric_finding (result_id, severity);

if not exists (select 1 from sys.indexes where name = 'ix_rle_rubric' and object_id = object_id('rubric_lifecycle_event'))
    create index ix_rle_rubric on rubric_lifecycle_event (rubric_id, occurred_at);

-- ---------------------------------------------------------------------------
-- Foreign keys
-- ---------------------------------------------------------------------------

if not exists (select 1 from sys.foreign_keys where name = 'fk_rubric_version_rubric')
begin
    alter table rubric_version
        add constraint fk_rubric_version_rubric
            foreign key (rubric_id) references rubric;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_check_rubric_version')
begin
    alter table rubric_check
        add constraint fk_check_rubric_version
            foreign key (rubric_version_id) references rubric_version;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_facility_override_rubric')
begin
    alter table facility_override
        add constraint fk_facility_override_rubric
            foreign key (rubric_id) references rubric;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric')
begin
    alter table rubric_result
        add constraint fk_result_rubric
            foreign key (rubric_id) references rubric;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_result_rubric_version')
begin
    alter table rubric_result
        add constraint fk_result_rubric_version
            foreign key (rubric_version_id) references rubric_version;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_result_facility_override')
begin
    alter table rubric_result
        add constraint fk_result_facility_override
            foreign key (facility_override_id) references facility_override;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_finding_result')
begin
    alter table rubric_finding
        add constraint fk_finding_result
            foreign key (result_id) references rubric_result;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_finding_check')
begin
    alter table rubric_finding
        add constraint fk_finding_check
            foreign key (check_id) references rubric_check;
end;

if not exists (select 1 from sys.foreign_keys where name = 'fk_lifecycle_event_rubric')
begin
    alter table rubric_lifecycle_event
        add constraint fk_lifecycle_event_rubric
            foreign key (rubric_id) references rubric;
end;
