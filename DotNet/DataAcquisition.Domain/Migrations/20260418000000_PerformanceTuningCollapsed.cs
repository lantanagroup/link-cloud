using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Single collapsed migration for the DataAcquisition performance-tuning branch.
    ///
    /// - All preparatory steps are fully idempotent and resilient.
    /// - Final CreateIndex / AddForeignKey calls are wrapped so they do NOT fail the migration
    ///   if the index or FK already exists (exactly what you asked for).
    ///   They will still fail the migration for any other error.
    /// </summary>
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260418000000_PerformanceTuningCollapsed")]
    public partial class PerformanceTuningCollapsed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. RCSI
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) =
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);

            // 2. ScheduledReports + column + data migration (idempotent)
            migrationBuilder.Sql(@"
                IF OBJECT_ID('ScheduledReports') IS NULL
                BEGIN
                    CREATE TABLE [ScheduledReports] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [ReportTrackingId] nvarchar(256) NOT NULL,
                        [Frequency] nvarchar(50) NOT NULL,
                        [StartDate] datetime2 NOT NULL,
                        [EndDate] datetime2 NOT NULL,
                        [ReportTypes] nvarchar(2000) NULL,
                        [CreateDate] datetime2 NOT NULL,
                        CONSTRAINT [PK_ScheduledReports] PRIMARY KEY CLUSTERED ([Id])
                    );
                    CREATE UNIQUE NONCLUSTERED INDEX [UX_ScheduledReports_ReportTrackingId] ON [ScheduledReports]([ReportTrackingId]);
                END

                IF COL_LENGTH('DataAcquisitionLog', 'ScheduledReportId') IS NULL
                    ALTER TABLE [DataAcquisitionLog] ADD [ScheduledReportId] bigint NULL;

                IF COL_LENGTH('DataAcquisitionLog', 'ScheduledReport')   IS NOT NULL
                   AND COL_LENGTH('DataAcquisitionLog', 'ReportStartDate')  IS NOT NULL
                   AND COL_LENGTH('DataAcquisitionLog', 'ReportEndDate')    IS NOT NULL
                   AND COL_LENGTH('DataAcquisitionLog', 'ScheduledReportId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        INSERT INTO [ScheduledReports] ([ReportTrackingId], [Frequency], [StartDate], [EndDate], [ReportTypes], [CreateDate])
                        SELECT
                            dal.[ReportTrackingId],
                            COALESCE(NULLIF(JSON_VALUE(dal.[ScheduledReport], ''$.Frequency''), ''''), ''Adhoc''),
                            COALESCE(TRY_CAST(JSON_VALUE(dal.[ScheduledReport], ''$.StartDate'') AS datetime2), dal.[ReportStartDate], ''1900-01-01''),
                            COALESCE(TRY_CAST(JSON_VALUE(dal.[ScheduledReport], ''$.EndDate'')   AS datetime2), dal.[ReportEndDate],   ''1900-01-01''),
                            (SELECT STRING_AGG(rt.[value], '','') FROM OPENJSON(dal.[ScheduledReport], ''$.ReportTypes'') rt),
                            GETUTCDATE()
                        FROM (
                            SELECT [ReportTrackingId], [ScheduledReport], [ReportStartDate], [ReportEndDate],
                                   ROW_NUMBER() OVER (PARTITION BY [ReportTrackingId] ORDER BY [Id]) AS rn
                            FROM [DataAcquisitionLog]
                            WHERE [ScheduledReport] IS NOT NULL AND [ReportTrackingId] IS NOT NULL
                        ) dal
                        WHERE dal.rn = 1
                          AND NOT EXISTS (SELECT 1 FROM [ScheduledReports] sr WHERE sr.[ReportTrackingId] = dal.[ReportTrackingId]);
                    ');

                    EXEC(N'
                        UPDATE dal
                        SET dal.[ScheduledReportId] = sr.[Id]
                        FROM [DataAcquisitionLog] dal
                        INNER JOIN [ScheduledReports] sr ON dal.[ReportTrackingId] = sr.[ReportTrackingId]
                        WHERE dal.[ScheduledReport] IS NOT NULL AND dal.[ScheduledReportId] IS NULL;
                    ');
                END
            ");

            // 3. DataAcquisitionLogNotes (idempotent)
            migrationBuilder.Sql(@"
                IF OBJECT_ID('DataAcquisitionLogNotes') IS NULL
                BEGIN
                    CREATE TABLE [DataAcquisitionLogNotes] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [DataAcquisitionLogId] bigint NOT NULL,
                        [Note] nvarchar(max) NOT NULL,
                        [CreateDate] datetime2 NOT NULL,
                        CONSTRAINT [PK_DataAcquisitionLogNotes] PRIMARY KEY CLUSTERED ([Id]),
                        CONSTRAINT [FK_DataAcquisitionLogNotes_DataAcquisitionLog_DataAcquisitionLogId]
                            FOREIGN KEY ([DataAcquisitionLogId]) REFERENCES [DataAcquisitionLog]([Id]) ON DELETE CASCADE
                    );
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogNotes_DataAcquisitionLogId] ON [DataAcquisitionLogNotes]([DataAcquisitionLogId]);
                END

                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    EXEC(N'
                        INSERT INTO [DataAcquisitionLogNotes]
                        SELECT dal.[Id], CAST(j.[value] AS nvarchar(max)), GETUTCDATE()
                        FROM [DataAcquisitionLog] dal
                        CROSS APPLY OPENJSON(dal.[Notes]) j
                        WHERE dal.[Notes] IS NOT NULL AND ISJSON(dal.[Notes]) = 1
                          AND NOT EXISTS (SELECT 1 FROM [DataAcquisitionLogNotes] n
                                          WHERE n.[DataAcquisitionLogId] = dal.[Id] AND n.[Note] = CAST(j.[value] AS nvarchar(max)));
                    ');
                END
            ");

            // 4. DataAcquisitionLogResourceIds (idempotent)
            migrationBuilder.Sql(@"
                IF OBJECT_ID('DataAcquisitionLogResourceIds') IS NULL
                BEGIN
                    CREATE TABLE [DataAcquisitionLogResourceIds] (
                        [Id] bigint IDENTITY(1,1) NOT NULL,
                        [DataAcquisitionLogId] bigint NOT NULL,
                        [ResourceId] nvarchar(512) NOT NULL,
                        [CreateDate] datetime2 NOT NULL,
                        CONSTRAINT [PK_DataAcquisitionLogResourceIds] PRIMARY KEY CLUSTERED ([Id]),
                        CONSTRAINT [FK_DataAcquisitionLogResourceIds_DataAcquisitionLog_DataAcquisitionLogId]
                            FOREIGN KEY ([DataAcquisitionLogId]) REFERENCES [DataAcquisitionLog]([Id]) ON DELETE CASCADE
                    );
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogResourceIds_DataAcquisitionLogId] ON [DataAcquisitionLogResourceIds]([DataAcquisitionLogId]);
                END

                IF COL_LENGTH('DataAcquisitionLog', 'ResourceAcquiredIds') IS NOT NULL
                BEGIN
                    EXEC(N'
                        INSERT INTO [DataAcquisitionLogResourceIds]
                        SELECT dal.[Id], j.[value], GETUTCDATE()
                        FROM [DataAcquisitionLog] dal
                        CROSS APPLY OPENJSON(dal.[ResourceAcquiredIds]) j
                        WHERE dal.[ResourceAcquiredIds] IS NOT NULL
                          AND dal.[ResourceAcquiredIds] <> ''[]''
                          AND dal.[ResourceAcquiredIds] <> ''null''
                          AND NOT EXISTS (SELECT 1 FROM [DataAcquisitionLogResourceIds] r
                                          WHERE r.[DataAcquisitionLogId] = dal.[Id] AND r.[ResourceId] = j.[value]);
                    ');
                END
            ");

            // 5. SiblingCount
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'SiblingCount') IS NULL
                    ALTER TABLE [DataAcquisitionLog] ADD [SiblingCount] int NULL;
            ");

            // 6. ReferenceResources — max-length columns, unique index, junction table
            //    All operations are now raw SQL + fully idempotent to prevent EF Core index conflicts in tests

            // 6a. Drop existing index FIRST (defensive, safe even if index was never created)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes 
                           WHERE object_id = OBJECT_ID('ReferenceResources') 
                             AND name = 'IX_ReferenceResources_Facility_Type_ResourceId')
                    DROP INDEX [IX_ReferenceResources_Facility_Type_ResourceId] ON [ReferenceResources];
            ");

            // 6b. Alter columns using raw SQL (EF fluent AlterColumn was triggering the internal DROP)
            migrationBuilder.Sql(@"
                -- Shorten columns (no-op and safe if they are already the new lengths)
                ALTER TABLE [ReferenceResources] ALTER COLUMN [FacilityId] nvarchar(256) NOT NULL;
                ALTER TABLE [ReferenceResources] ALTER COLUMN [ResourceId] nvarchar(256) NOT NULL;
                ALTER TABLE [ReferenceResources] ALTER COLUMN [ResourceType] nvarchar(128) NOT NULL;
            ");

            // 6c. Junction table (idempotent)
            migrationBuilder.Sql(@"
                IF OBJECT_ID('DataAcquisitionLogReferenceResource') IS NULL
                BEGIN
                    CREATE TABLE [DataAcquisitionLogReferenceResource] (
                        [DataAcquisitionLogId] bigint NOT NULL,
                        [ReferenceResourceId] uniqueidentifier NOT NULL,
                        CONSTRAINT [PK_DataAcquisitionLogReferenceResource] PRIMARY KEY CLUSTERED ([DataAcquisitionLogId], [ReferenceResourceId]),
                        CONSTRAINT [FK_DataAcquisitionLogReferenceResource_DataAcquisitionLog_DataAcquisitionLogId] FOREIGN KEY ([DataAcquisitionLogId]) REFERENCES [DataAcquisitionLog]([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_DataAcquisitionLogReferenceResource_ReferenceResources_ReferenceResourceId] FOREIGN KEY ([ReferenceResourceId]) REFERENCES [ReferenceResources]([Id]) ON DELETE CASCADE
                    );
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogReferenceResource_ReferenceResourceId] ON [DataAcquisitionLogReferenceResource]([ReferenceResourceId]);
                END
            ");

            // 6d. Migrate old FK data + robust drop (unchanged)
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ReferenceResources', 'DataAcquisitionLogId') IS NOT NULL
                BEGIN
                    EXEC(N'
                        INSERT INTO [DataAcquisitionLogReferenceResource]
                        SELECT DISTINCT [DataAcquisitionLogId], [Id]
                        FROM [ReferenceResources]
                        WHERE [DataAcquisitionLogId] IS NOT NULL
                          AND NOT EXISTS (SELECT 1 FROM [DataAcquisitionLogReferenceResource] j
                                          WHERE j.[DataAcquisitionLogId] = [ReferenceResources].[DataAcquisitionLogId]
                                            AND j.[ReferenceResourceId] = [ReferenceResources].[Id]);
                    ');

                    DECLARE @fkName sysname;
                    DECLARE @dropSql nvarchar(500);
                    WHILE 1 = 1
                    BEGIN
                        SET @fkName = NULL;
                        SELECT TOP 1 @fkName = fk.name
                        FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                        WHERE fk.parent_object_id = OBJECT_ID('ReferenceResources') AND c.name = 'DataAcquisitionLogId';

                        IF @fkName IS NULL BREAK;

                        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = @fkName)
                        BEGIN
                            SET @dropSql = N'ALTER TABLE [ReferenceResources] DROP CONSTRAINT [' + @fkName + N']';
                            EXEC sp_executesql @dropSql;
                        END
                    END

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ReferenceResources') AND name = 'IX_ReferenceResources_DataAcquisitionLogId')
                        DROP INDEX [IX_ReferenceResources_DataAcquisitionLogId] ON [ReferenceResources];

                    ALTER TABLE [ReferenceResources] DROP COLUMN [DataAcquisitionLogId];
                END
            ");

            // 6e. Deduplication (robust version from earlier fix)
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReferenceResources] GROUP BY [FacilityId], [ResourceType], [ResourceId] HAVING COUNT(*) > 1)
                BEGIN
                    IF OBJECT_ID('tempdb..#ReferenceResourceDedupMap') IS NOT NULL DROP TABLE #ReferenceResourceDedupMap;

                    ;WITH Ranked AS (
                        SELECT rr.[Id], rr.[FacilityId], rr.[ResourceType], rr.[ResourceId],
                               ROW_NUMBER() OVER (PARTITION BY rr.[FacilityId], rr.[ResourceType], rr.[ResourceId] ORDER BY rr.[CreateDate] ASC, rr.[Id] ASC) AS rn,
                               FIRST_VALUE(rr.[Id]) OVER (PARTITION BY rr.[FacilityId], rr.[ResourceType], rr.[ResourceId] ORDER BY rr.[CreateDate] ASC, rr.[Id] ASC) AS [KeeperId]
                        FROM [ReferenceResources] rr
                    )
                    SELECT r.[Id] AS [DuplicateId], r.[KeeperId]
                    INTO #ReferenceResourceDedupMap
                    FROM Ranked r WHERE r.rn > 1;

                    IF OBJECT_ID('DataAcquisitionLogReferenceResource') IS NOT NULL
                    BEGIN
                        IF OBJECT_ID('tempdb..#TempJunctionRemap') IS NOT NULL DROP TABLE #TempJunctionRemap;
                        
                        SELECT DISTINCT dalrr.[DataAcquisitionLogId], m.[KeeperId]
                        INTO #TempJunctionRemap
                        FROM [DataAcquisitionLogReferenceResource] dalrr
                        INNER JOIN #ReferenceResourceDedupMap m 
                            ON dalrr.[ReferenceResourceId] = m.[DuplicateId];

                        DELETE dalrr 
                        FROM [DataAcquisitionLogReferenceResource] dalrr
                        INNER JOIN #ReferenceResourceDedupMap m 
                            ON dalrr.[ReferenceResourceId] = m.[DuplicateId];

                        INSERT INTO [DataAcquisitionLogReferenceResource] ([DataAcquisitionLogId], [ReferenceResourceId])
                        SELECT tj.[DataAcquisitionLogId], tj.[KeeperId]
                        FROM #TempJunctionRemap tj
                        WHERE NOT EXISTS (
                            SELECT 1 
                            FROM [DataAcquisitionLogReferenceResource] existing
                            WHERE existing.[DataAcquisitionLogId] = tj.[DataAcquisitionLogId]
                              AND existing.[ReferenceResourceId] = tj.[KeeperId]
                        );

                        DROP TABLE #TempJunctionRemap;
                    END

                    DELETE rr FROM [ReferenceResources] rr 
                    INNER JOIN #ReferenceResourceDedupMap m ON rr.[Id] = m.[DuplicateId];
                END
            ");

            // 6f. Create unique index (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes 
                               WHERE object_id = OBJECT_ID('ReferenceResources') 
                                 AND name = 'IX_ReferenceResources_Facility_Type_ResourceId')
                    CREATE UNIQUE NONCLUSTERED INDEX [IX_ReferenceResources_Facility_Type_ResourceId]
                    ON [ReferenceResources] ([FacilityId], [ResourceType], [ResourceId]);
            ");

            // 7. Drop old JSON columns safely
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_TailSent_Status')
                    DROP INDEX [IX_DataAcquisitionLogs_TailSent_Status] ON [DataAcquisitionLog];

                IF COL_LENGTH('DataAcquisitionLog', 'ScheduledReport') IS NOT NULL
                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [ScheduledReport];

                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    DECLARE @dfName sysname;
                    SELECT @dfName = dc.[name] FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'Notes';
                    IF @dfName IS NOT NULL EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName + ']');
                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [Notes];
                END

                IF COL_LENGTH('DataAcquisitionLog', 'ResourceAcquiredIds') IS NOT NULL
                BEGIN
                    DECLARE @dfName2 sysname;
                    SELECT @dfName2 = dc.[name] FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'ResourceAcquiredIds';
                    IF @dfName2 IS NOT NULL EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName2 + ']');
                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [ResourceAcquiredIds];
                END
            ");

            // 8. Final indexes and FK — now idempotent (will not fail migration if they already exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id]
                    ON [DataAcquisitionLog] ([FacilityId], [Status], [ExecutionDate], [Id])
                    INCLUDE ([Priority], [IsCensus], [PatientId], [ReportableEvent],
                             [ReportTrackingId], [CorrelationId], [FhirVersion],
                             [QueryType], [QueryPhase], [TraceId], [RetryAttempts],
                             [CompletionDate], [CompletionTimeMilliseconds]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted]
                    ON [DataAcquisitionLog] ([ReportTrackingId], [IsDeleted])
                    INCLUDE ([PatientId], [Status], [RetryAttempts], [CompletionTimeMilliseconds]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_IsDeleted_Id')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_IsDeleted_Id]
                    ON [DataAcquisitionLog] ([IsDeleted], [Id])
                    INCLUDE ([Priority], [FacilityId], [PatientId], [ReportTrackingId],
                             [FhirVersion], [QueryType], [QueryPhase],
                             [ExecutionDate], [CreateDate], [RetryAttempts], [Status]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_TailSent_Status')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_TailSent_Status]
                    ON [DataAcquisitionLog] ([TailSent], [Status])
                    INCLUDE ([FacilityId], [ReportTrackingId], [CorrelationId],
                             [ReportStartDate], [ReportEndDate], [QueryPhase],
                             [TraceId], [PatientId], [ReportableEvent], [ScheduledReportId]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_InlineTail')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_InlineTail]
                    ON [DataAcquisitionLog] ([TailSent], [SiblingCount], [FacilityId], [CorrelationId], [QueryPhase], [Status])
                    WHERE [TailSent] = 0 AND [SiblingCount] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [QueryPhase] IS NOT NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLog_ScheduledReportId')
                    CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLog_ScheduledReportId]
                    ON [DataAcquisitionLog] ([ScheduledReportId]);

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DataAcquisitionLog_ScheduledReports_ScheduledReportId')
                    ALTER TABLE [DataAcquisitionLog]
                        ADD CONSTRAINT [FK_DataAcquisitionLog_ScheduledReports_ScheduledReportId]
                            FOREIGN KEY ([ScheduledReportId])
                            REFERENCES [ScheduledReports] ([Id])
                            ON DELETE SET NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ?? Reverse order ??

            // Drop FK and new indexes
            migrationBuilder.DropForeignKey(
                name: "FK_DataAcquisitionLog_ScheduledReports_ScheduledReportId",
                table: "DataAcquisitionLog");

            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLog_ScheduledReportId", table: "DataAcquisitionLog");
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_InlineTail", table: "DataAcquisitionLog");
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_TailSent_Status", table: "DataAcquisitionLog");
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_IsDeleted_Id", table: "DataAcquisitionLog");
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted", table: "DataAcquisitionLog");
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id", table: "DataAcquisitionLog");

            // Re-add the three JSON columns
            migrationBuilder.AddColumn<string>(
                name: "ResourceAcquiredIds",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "ScheduledReport",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            // Restore data from child tables back to JSON columns
            migrationBuilder.Sql(@"
                -- Restore ResourceAcquiredIds
                UPDATE dal
                SET dal.[ResourceAcquiredIds] = COALESCE(
                    (SELECT '[' + STRING_AGG('""' + STRING_ESCAPE(r.[ResourceId], 'json') + '""', ',') + ']'
                     FROM [DataAcquisitionLogResourceIds] r WHERE r.[DataAcquisitionLogId] = dal.[Id]),
                    '[]')
                FROM [DataAcquisitionLog] dal;

                -- Restore Notes
                UPDATE dal
                SET dal.[Notes] = COALESCE(
                    (SELECT '[' + STRING_AGG('""' + STRING_ESCAPE(n.[Note], 'json') + '""', ',') + ']'
                     FROM [DataAcquisitionLogNotes] n WHERE n.[DataAcquisitionLogId] = dal.[Id]),
                    '[]')
                FROM [DataAcquisitionLog] dal;

                -- Restore ScheduledReport JSON
                UPDATE dal
                SET dal.[ScheduledReport] = CONCAT(
                    '{',
                    '""ReportTrackingId"":""', STRING_ESCAPE(sr.[ReportTrackingId], 'json'), '""',
                    ',""Frequency"":""', STRING_ESCAPE(sr.[Frequency], 'json'), '""',
                    ',""StartDate"":""', CONVERT(nvarchar(30), sr.[StartDate], 127), '""',
                    ',""EndDate"":""', CONVERT(nvarchar(30), sr.[EndDate], 127), '""',
                    ',""ReportTypes"":[',
                        ISNULL((SELECT STRING_AGG(CONCAT('""', STRING_ESCAPE(LTRIM(RTRIM(value)), 'json'), '""'), ',')
                                FROM STRING_SPLIT(sr.[ReportTypes], ',')), ''),
                    ']',
                    '}')
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ScheduledReportId] = sr.[Id];
            ");

            // Recreate the old covering indexes with the JSON columns
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "Status", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "IsCensus", "PatientId", "ReportableEvent",
                    "ReportTrackingId", "CorrelationId", "FhirVersion",
                    "QueryType", "QueryPhase", "TraceId", "RetryAttempts",
                    "CompletionDate", "CompletionTimeMilliseconds",
                    "ResourceAcquiredIds", "Notes", "ScheduledReport"
                });

            // Drop SiblingCount
            migrationBuilder.DropColumn(name: "SiblingCount", table: "DataAcquisitionLog");

            // Drop ScheduledReportId and ScheduledReports table
            migrationBuilder.DropColumn(name: "ScheduledReportId", table: "DataAcquisitionLog");
            migrationBuilder.DropTable(name: "ScheduledReports");

            // Drop child tables
            migrationBuilder.DropTable(name: "DataAcquisitionLogResourceIds");
            migrationBuilder.DropTable(name: "DataAcquisitionLogNotes");

            // Restore ReferenceResources: unique index ? non-unique, re-add FK column
            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources");

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId", table: "ReferenceResources",
                type: "nvarchar(max)", nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(256)", oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ResourceId", table: "ReferenceResources",
                type: "nvarchar(max)", nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(256)", oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ResourceType", table: "ReferenceResources",
                type: "nvarchar(max)", nullable: false,
                oldClrType: typeof(string), oldType: "nvarchar(128)", oldMaxLength: 128);

            migrationBuilder.AddColumn<long>(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id");

            // Restore data from junction table
            migrationBuilder.Sql(@"
                UPDATE rr
                SET rr.[DataAcquisitionLogId] = j.[DataAcquisitionLogId]
                FROM [ReferenceResources] rr
                INNER JOIN (
                    SELECT [ReferenceResourceId], MIN([DataAcquisitionLogId]) AS [DataAcquisitionLogId]
                    FROM [DataAcquisitionLogReferenceResource]
                    GROUP BY [ReferenceResourceId]
                ) j ON rr.[Id] = j.[ReferenceResourceId];
            ");

            // Drop junction table (after data has been restored from it)
            migrationBuilder.DropTable(name: "DataAcquisitionLogReferenceResource");

            // RCSI off
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);
        }
    }
}
