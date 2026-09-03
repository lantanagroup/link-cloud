-- ADR-0003 shadow-run: legacy engine audit trail (legacy_shadow_result/legacy_shadow_finding, mirroring
-- rubric_result/rubric_finding) and the parallel-run diff summary (shadow_comparison_result).
--
-- request_id columns carry the same id already generated for the corresponding rubric_result row
-- (rubric_result.request_id, uq_result_request) whenever the rubric engine was the primary/authoritative
-- run and the row below was only for comparison. Indexed, not unique -- unlike rubric_result, a request_id
-- here is only ever populated in that one direction, and stays null for the other (legacy-authoritative)
-- shadow direction. shadow_comparison_result.request_id backs the temporary "fetch shadow comparisons by
-- request id" API.

if not exists (select 1 from sys.tables where name = 'legacy_shadow_result' and schema_id = schema_id('dbo'))
begin
    create table legacy_shadow_result
    (
        result_id         uniqueidentifier  not null,
        correlation_id    varchar(128),
        facility_id       varchar(128)      not null,
        patient_id        varchar(128)      not null,
        report_id         varchar(128)      not null,
        fatal_count       int               not null,
        error_count       int               not null,
        warning_count     int               not null,
        information_count int               not null,
        requested_at      datetimeoffset(6) not null,
        completed_at      datetimeoffset(6) not null,
        duration_ms       bigint            not null,
        request_id        uniqueidentifier  null,
        primary key (result_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'legacy_shadow_finding' and schema_id = schema_id('dbo'))
begin
    create table legacy_shadow_finding
    (
        finding_id        uniqueidentifier not null,
        result_id         uniqueidentifier not null,
        severity          varchar(16)      not null check (severity in ('FATAL', 'ERROR', 'WARNING', 'INFORMATION', 'NULL')),
        code              varchar(32)      not null,
        message           varchar(max)     not null,
        location          varchar(512),
        expression        varchar(max),
        category_ids_json varchar(max),
        acceptable        bit,
        request_id        uniqueidentifier null,
        primary key (finding_id)
    );
end;

if not exists (select 1 from sys.tables where name = 'shadow_comparison_result' and schema_id = schema_id('dbo'))
begin
    create table shadow_comparison_result
    (
        id                     uniqueidentifier  not null,
        correlation_id         varchar(128),
        facility_id            varchar(128)      not null,
        patient_id             varchar(128)      not null,
        report_id              varchar(128)      not null,
        rubric_id              varchar(128),
        ran_new_engine         bit               not null,
        matched                bit               not null,
        added_count            int               not null,
        missing_count          int               not null,
        severity_changed_count int               not null,
        matched_finding_count  int               not null,
        added_json             varchar(max),
        missing_json           varchar(max),
        severity_changed_json  varchar(max),
        compared_at            datetimeoffset(6) not null,
        request_id             uniqueidentifier  null,
        primary key (id)
    );
end;

-- ---------------------------------------------------------------------------
-- Indexes
-- ---------------------------------------------------------------------------

if not exists (select 1 from sys.indexes where name = 'ix_legacy_shadow_result_facility_report'
                 and object_id = object_id('legacy_shadow_result'))
    create index ix_legacy_shadow_result_facility_report
        on legacy_shadow_result (facility_id, report_id);

if not exists (select 1 from sys.indexes where name = 'ix_legacy_shadow_result_correlation'
                 and object_id = object_id('legacy_shadow_result'))
    create index ix_legacy_shadow_result_correlation
        on legacy_shadow_result (correlation_id);

if not exists (select 1 from sys.indexes where name = 'ix_legacy_shadow_result_request'
                 and object_id = object_id('legacy_shadow_result'))
    create index ix_legacy_shadow_result_request
        on legacy_shadow_result (request_id);

if not exists (select 1 from sys.indexes where name = 'ix_legacy_shadow_finding_result'
                 and object_id = object_id('legacy_shadow_finding'))
    create index ix_legacy_shadow_finding_result
        on legacy_shadow_finding (result_id);

if not exists (select 1 from sys.indexes where name = 'ix_legacy_shadow_finding_request'
                 and object_id = object_id('legacy_shadow_finding'))
    create index ix_legacy_shadow_finding_request
        on legacy_shadow_finding (request_id);

if not exists (select 1 from sys.indexes where name = 'ix_shadow_comparison_facility_report'
                 and object_id = object_id('shadow_comparison_result'))
    create index ix_shadow_comparison_facility_report
        on shadow_comparison_result (facility_id, report_id);

if not exists (select 1 from sys.indexes where name = 'ix_shadow_comparison_compared_at'
                 and object_id = object_id('shadow_comparison_result'))
    create index ix_shadow_comparison_compared_at
        on shadow_comparison_result (compared_at);

if not exists (select 1 from sys.indexes where name = 'ix_shadow_comparison_request'
                 and object_id = object_id('shadow_comparison_result'))
    create index ix_shadow_comparison_request
        on shadow_comparison_result (request_id);

-- ---------------------------------------------------------------------------
-- Foreign keys
-- ---------------------------------------------------------------------------

if not exists (select 1 from sys.foreign_keys where name = 'fk_legacy_shadow_finding_result')
begin
    alter table legacy_shadow_finding
        add constraint fk_legacy_shadow_finding_result
            foreign key (result_id) references legacy_shadow_result;
end;
