-- Two independent, DB-only performance changes, no app-code impact:
--
-- 1) PAGE compression on the append-only, high-volume result tables. Both have a lot of
--    repeated low-cardinality strings (status/severity/code/dimension) and grow unbounded,
--    so compression trades a small amount of CPU for meaningfully less I/O and storage.
--
-- 2) Force large JSON columns off-row so they don't get stored in-line on data pages when
--    small enough to fit; keeps the hot row (used for scans/filters) compact regardless of
--    how large a given score/definition JSON blob happens to be.

set quoted_identifier on;

if exists (select 1 from sys.tables where name = 'rubric_result' and schema_id = schema_id('dbo'))
begin
    exec sp_tableoption 'dbo.rubric_result', 'large value types out of row', 1;
    alter index all on rubric_result rebuild with (data_compression = page);
end;

if exists (select 1 from sys.tables where name = 'rubric_finding' and schema_id = schema_id('dbo'))
begin
    exec sp_tableoption 'dbo.rubric_finding', 'large value types out of row', 1;
    alter index all on rubric_finding rebuild with (data_compression = page);
end;

if exists (select 1 from sys.tables where name = 'rubric_version' and schema_id = schema_id('dbo'))
begin
    exec sp_tableoption 'dbo.rubric_version', 'large value types out of row', 1;
    alter index all on rubric_version rebuild with (data_compression = page);
end;

if exists (select 1 from sys.tables where name = 'rubric_check' and schema_id = schema_id('dbo'))
begin
    exec sp_tableoption 'dbo.rubric_check', 'large value types out of row', 1;
    alter index all on rubric_check rebuild with (data_compression = page);
end;
