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
    /// Net schema changes from the pre-branch baseline:
    /// 
    ///  1. RCSI — Enable Read Committed Snapshot Isolation (eliminates reader/writer deadlocks).
    ///  2. ScheduledReports — Normalise ScheduledReport JSON ? dedicated table + FK on DataAcquisitionLog.
    ///  3. DataAcquisitionLogNotes — Normalise Notes JSON ? dedicated child table.
    ///  4. DataAcquisitionLogResourceIds — Normalise ResourceAcquiredIds JSON ? dedicated child table.
    ///  5. ReferenceResources — Max-length columns, unique composite index, remove DataAcquisitionLogId FK.
    ///  6. DataAcquisitionLogReferenceResource — Junction table for the many-to-many relationship.
    ///  7. SiblingCount column + IX_DataAcquisitionLogs_InlineTail index.
    ///  8. New performance indexes on DataAcquisitionLog.
    ///  9. Rebuild existing covering indexes to exclude the dropped columns.
    /// </summary>
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260418000000_PerformanceTuningCollapsed")]
    public partial class PerformanceTuningCollapsed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ???????????????????????????????????????????????????????????????
            // 1. RCSI
            // ???????????????????????????????????????????????????????????????
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);

            // ???????????????????????????????????????????????????????????????
            // 2. ScheduledReports table + FK on DataAcquisitionLog
            // ???????????????????????????????????????????????????????????????
            migrationBuilder.CreateTable(
                name: "ScheduledReports",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportTrackingId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReportTypes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ScheduledReports_ReportTrackingId",
                table: "ScheduledReports",
                column: "ReportTrackingId",
                unique: true);

            migrationBuilder.AddColumn<long>(
                name: "ScheduledReportId",
                table: "DataAcquisitionLog",
                type: "bigint",
                nullable: true);

            // Populate ScheduledReports from existing JSON
            migrationBuilder.Sql(@"
                INSERT INTO [ScheduledReports] ([ReportTrackingId], [Frequency], [StartDate], [EndDate], [ReportTypes], [CreateDate])
                SELECT
                    dal.[ReportTrackingId],
                    COALESCE(NULLIF(JSON_VALUE(dal.[ScheduledReport], '$.Frequency'), ''), 'Adhoc'),
                    COALESCE(TRY_CAST(JSON_VALUE(dal.[ScheduledReport], '$.StartDate') AS datetime2), dal.[ReportStartDate], '1900-01-01'),
                    COALESCE(TRY_CAST(JSON_VALUE(dal.[ScheduledReport], '$.EndDate') AS datetime2), dal.[ReportEndDate], '1900-01-01'),
                    (
                        SELECT STRING_AGG(rt.[value], ',')
                        FROM OPENJSON(dal.[ScheduledReport], '$.ReportTypes') rt
                    ),
                    GETUTCDATE()
                FROM (
                    SELECT
                        [ReportTrackingId], [ScheduledReport], [ReportStartDate], [ReportEndDate],
                        ROW_NUMBER() OVER (PARTITION BY [ReportTrackingId] ORDER BY [Id]) AS rn
                    FROM [DataAcquisitionLog]
                    WHERE [ScheduledReport] IS NOT NULL AND [ReportTrackingId] IS NOT NULL
                ) dal
                WHERE dal.rn = 1;

                UPDATE dal
                SET dal.[ScheduledReportId] = sr.[Id]
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ReportTrackingId] = sr.[ReportTrackingId]
                WHERE dal.[ScheduledReport] IS NOT NULL;
            ");

            // ???????????????????????????????????????????????????????????????
            // 3. DataAcquisitionLogNotes table
            // ???????????????????????????????????????????????????????????????
            migrationBuilder.CreateTable(
                name: "DataAcquisitionLogNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLogNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogNotes_DataAcquisitionLog_DataAcquisitionLogId",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogNotes_DataAcquisitionLogId",
                table: "DataAcquisitionLogNotes",
                column: "DataAcquisitionLogId");

            // Migrate Notes JSON ? child rows
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    INSERT INTO [DataAcquisitionLogNotes] ([DataAcquisitionLogId], [Note], [CreateDate])
                    SELECT dal.[Id], CAST(j.[value] AS nvarchar(max)), GETUTCDATE()
                    FROM [DataAcquisitionLog] dal
                    CROSS APPLY OPENJSON(dal.[Notes]) j
                    WHERE dal.[Notes] IS NOT NULL AND ISJSON(dal.[Notes]) = 1;
                END
            ");

            // ???????????????????????????????????????????????????????????????
            // 4. DataAcquisitionLogResourceIds table
            // ???????????????????????????????????????????????????????????????
            migrationBuilder.CreateTable(
                name: "DataAcquisitionLogResourceIds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLogResourceIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogResourceIds_DataAcquisitionLog_DataAcquisitionLogId",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogResourceIds_DataAcquisitionLogId",
                table: "DataAcquisitionLogResourceIds",
                column: "DataAcquisitionLogId");

            // Migrate ResourceAcquiredIds JSON ? child rows
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'ResourceAcquiredIds') IS NOT NULL
                BEGIN
                    INSERT INTO [DataAcquisitionLogResourceIds] ([DataAcquisitionLogId], [ResourceId], [CreateDate])
                    SELECT dal.[Id], j.[value], GETUTCDATE()
                    FROM [DataAcquisitionLog] dal
                    CROSS APPLY OPENJSON(dal.[ResourceAcquiredIds]) j
                    WHERE dal.[ResourceAcquiredIds] IS NOT NULL
                      AND dal.[ResourceAcquiredIds] <> '[]'
                      AND dal.[ResourceAcquiredIds] <> 'null';
                END
            ");

            // ???????????????????????????????????????????????????????????????
            // 5. SiblingCount column
            // ???????????????????????????????????????????????????????????????
            migrationBuilder.AddColumn<int>(
                name: "SiblingCount",
                table: "DataAcquisitionLog",
                type: "int",
                nullable: true);

            // ???????????????????????????????????????????????????????????????
            // 6. ReferenceResources — max-length columns, unique index, junction table
            // ???????????????????????????????????????????????????????????????

            // 6a. Alter columns from nvarchar(max) to bounded lengths
            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "ReferenceResources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceId",
                table: "ReferenceResources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceType",
                table: "ReferenceResources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // 6b. Create the junction table
            migrationBuilder.CreateTable(
                name: "DataAcquisitionLogReferenceResource",
                columns: table => new
                {
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLogReferenceResource", x => new { x.DataAcquisitionLogId, x.ReferenceResourceId });
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogReferenceResource_DataAcquisitionLog_DataAcquisitionLogId",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogReferenceResource_ReferenceResources_ReferenceResourceId",
                        column: x => x.ReferenceResourceId,
                        principalTable: "ReferenceResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogReferenceResource_ReferenceResourceId",
                table: "DataAcquisitionLogReferenceResource",
                column: "ReferenceResourceId");

            // 6c. Migrate existing FK relationships into junction table, then drop old FK/column
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ReferenceResources', 'DataAcquisitionLogId') IS NOT NULL
                BEGIN
                    INSERT INTO [DataAcquisitionLogReferenceResource] ([DataAcquisitionLogId], [ReferenceResourceId])
                    SELECT DISTINCT [DataAcquisitionLogId], [Id]
                    FROM [ReferenceResources]
                    WHERE [DataAcquisitionLogId] IS NOT NULL;

                    -- Drop FK constraints on DataAcquisitionLogId
                    DECLARE @fkName sysname;
                    DECLARE @sql nvarchar(500);

                    SELECT TOP 1 @fkName = fk.name
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                    WHERE fk.parent_object_id = OBJECT_ID('ReferenceResources')
                      AND c.name = 'DataAcquisitionLogId';

                    WHILE @fkName IS NOT NULL
                    BEGIN
                        SET @sql = N'ALTER TABLE [ReferenceResources] DROP CONSTRAINT [' + @fkName + N']';
                        EXEC sp_executesql @sql;

                        SET @fkName = NULL;
                        SELECT TOP 1 @fkName = fk.name
                        FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                        WHERE fk.parent_object_id = OBJECT_ID('ReferenceResources')
                          AND c.name = 'DataAcquisitionLogId';
                    END

                    -- Drop old index on DataAcquisitionLogId
                    IF EXISTS (
                        SELECT 1 FROM sys.indexes
                        WHERE object_id = OBJECT_ID('ReferenceResources')
                          AND name = 'IX_ReferenceResources_DataAcquisitionLogId')
                    BEGIN
                        DROP INDEX [IX_ReferenceResources_DataAcquisitionLogId] ON [ReferenceResources];
                    END

                    ALTER TABLE [ReferenceResources] DROP COLUMN [DataAcquisitionLogId];
                END
            ");

            // 6d. Create unique composite index (replaces old non-unique one if any)
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('ReferenceResources')
                      AND name = 'IX_ReferenceResources_Facility_Type_ResourceId')
                BEGIN
                    DROP INDEX [IX_ReferenceResources_Facility_Type_ResourceId] ON [ReferenceResources];
                END
            ");

            // 6e. Safety dedupe for legacy duplicate ReferenceResources rows.
            //     Keep one row per (FacilityId, ResourceType, ResourceId), re-point junction rows,
            //     then remove duplicates before creating the unique index.
            migrationBuilder.Sql(@"
                 IF EXISTS (
                    SELECT 1
                    FROM [ReferenceResources]
                    GROUP BY [FacilityId], [ResourceType], [ResourceId]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    IF OBJECT_ID('tempdb..#ReferenceResourceDedupMap') IS NOT NULL
                        DROP TABLE #ReferenceResourceDedupMap;

                    ;WITH Ranked AS (
                        SELECT
                            rr.[Id],
                            rr.[FacilityId],
                            rr.[ResourceType],
                            rr.[ResourceId],
                            ROW_NUMBER() OVER (
                                PARTITION BY rr.[FacilityId], rr.[ResourceType], rr.[ResourceId]
                                ORDER BY rr.[CreateDate] ASC, rr.[Id] ASC
                            ) AS rn,
                            -- This is the fix: use the SAME ordering as ROW_NUMBER
                            FIRST_VALUE(rr.[Id]) OVER (
                                PARTITION BY rr.[FacilityId], rr.[ResourceType], rr.[ResourceId]
                                ORDER BY rr.[CreateDate] ASC, rr.[Id] ASC
                            ) AS [KeeperId]
                        FROM [ReferenceResources] rr
                    )
                    SELECT
                        r.[Id] AS [DuplicateId],
                        r.[KeeperId]
                    INTO #ReferenceResourceDedupMap
                    FROM Ranked r
                    WHERE r.rn > 1;

                    IF OBJECT_ID('DataAcquisitionLogReferenceResource') IS NOT NULL
                    BEGIN
                        -- 1. Remove any rows that would violate the unique/PK constraint after remapping
                        DELETE dalrr
                        FROM [DataAcquisitionLogReferenceResource] dalrr
                        INNER JOIN #ReferenceResourceDedupMap m
                            ON dalrr.[ReferenceResourceId] = m.[DuplicateId]
                        WHERE EXISTS (
                            SELECT 1
                            FROM [DataAcquisitionLogReferenceResource] existing
                            WHERE existing.[DataAcquisitionLogId] = dalrr.[DataAcquisitionLogId]
                              AND existing.[ReferenceResourceId] = m.[KeeperId]
                        );

                        -- 2. Remap the rest to the keeper
                        UPDATE dalrr
                        SET dalrr.[ReferenceResourceId] = m.[KeeperId]
                        FROM [DataAcquisitionLogReferenceResource] dalrr
                        INNER JOIN #ReferenceResourceDedupMap m
                            ON dalrr.[ReferenceResourceId] = m.[DuplicateId];
                    END

                    -- 3. Finally delete the duplicate rows (now safe)
                    DELETE rr
                    FROM [ReferenceResources] rr
                    INNER JOIN #ReferenceResourceDedupMap m
                        ON rr.[Id] = m.[DuplicateId];
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources",
                columns: new[] { "FacilityId", "ResourceType", "ResourceId" },
                unique: true);

            // ???????????????????????????????????????????????????????????????
            // 7. Drop old JSON columns and rebuild all affected indexes
            //    Order: drop indexes ? drop columns ? recreate indexes
            // ???????????????????????????????????????????????????????????????

            // 7a. Drop existing indexes that include any of the columns being removed
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('DataAcquisitionLog')
                      AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                BEGIN
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];
                END

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE object_id = OBJECT_ID('DataAcquisitionLog')
                      AND name = 'IX_DataAcquisitionLogs_TailSent_Status')
                BEGIN
                    DROP INDEX [IX_DataAcquisitionLogs_TailSent_Status] ON [DataAcquisitionLog];
                END
            ");

            // 7b. Drop the ScheduledReport JSON column
            migrationBuilder.DropColumn(
                name: "ScheduledReport",
                table: "DataAcquisitionLog");

            // 7c. Drop the Notes JSON column (with default constraint cleanup)
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    DECLARE @dfName sysname;
                    SELECT @dfName = dc.[name]
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'Notes';

                    IF @dfName IS NOT NULL
                        EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName + ']');

                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [Notes];
                END
            ");

            // 7d. Drop the ResourceAcquiredIds JSON column
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'ResourceAcquiredIds') IS NOT NULL
                BEGIN
                    DECLARE @dfName2 sysname;
                    SELECT @dfName2 = dc.[name]
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'ResourceAcquiredIds';

                    IF @dfName2 IS NOT NULL
                        EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName2 + ']');

                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [ResourceAcquiredIds];
                END
            ");

            // ???????????????????????????????????????????????????????????????
            // 8. Recreate / create all indexes on DataAcquisitionLog
            //    (now that all columns are in their final state)
            // ???????????????????????????????????????????????????????????????

            // 8a. IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id
            //     — no ScheduledReport, Notes, or ResourceAcquiredIds in INCLUDE
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "Status", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "IsCensus", "PatientId", "ReportableEvent",
                    "ReportTrackingId", "CorrelationId", "FhirVersion",
                    "QueryType", "QueryPhase", "TraceId", "RetryAttempts",
                    "CompletionDate", "CompletionTimeMilliseconds"
                });

            // 8b. IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted (new)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] {
                    "PatientId", "Status", "RetryAttempts", "CompletionTimeMilliseconds"
                });

            // 8c. IX_DataAcquisitionLogs_IsDeleted_Id (new — default UI pagination)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_IsDeleted_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "IsDeleted", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "PatientId", "ReportTrackingId",
                    "FhirVersion", "QueryType", "QueryPhase",
                    "ExecutionDate", "CreateDate", "RetryAttempts", "Status"
                });

            // 8d. IX_DataAcquisitionLogs_TailSent_Status (new — tailing query)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_TailSent_Status",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "Status" })
                .Annotation("SqlServer:Include", new[] {
                    "FacilityId", "ReportTrackingId", "CorrelationId",
                    "ReportStartDate", "ReportEndDate", "QueryPhase",
                    "TraceId", "PatientId", "ReportableEvent", "ScheduledReportId"
                });

            // 8e. IX_DataAcquisitionLogs_InlineTail (new — inline tail completion check)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_InlineTail",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "SiblingCount", "FacilityId", "CorrelationId", "QueryPhase", "Status" },
                filter: "[TailSent] = 0 AND [SiblingCount] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [QueryPhase] IS NOT NULL");

            // 8f. FK index for ScheduledReportId
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLog_ScheduledReportId",
                table: "DataAcquisitionLog",
                column: "ScheduledReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_DataAcquisitionLog_ScheduledReports_ScheduledReportId",
                table: "DataAcquisitionLog",
                column: "ScheduledReportId",
                principalTable: "ScheduledReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
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
