using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Promotes ReportTrackingId to the natural primary key of ScheduledReports and
    /// makes DataAcquisitionLog.ReportTrackingId the FK, eliminating the synthetic
    /// bigint Id / ScheduledReportId indirection.
    ///
    /// Also removes the redundant ReportStartDate / ReportEndDate columns from
    /// DataAcquisitionLog (dates live in ScheduledReports via the FK).
    ///
    /// Migration steps:
    ///   1. Backfill orphaned ReportTrackingId values into ScheduledReports.
    ///   2. Drop all indexes and the FK that reference the old ScheduledReportId / Id columns.
    ///   3. Drop ScheduledReportId column from DataAcquisitionLog.
    ///   4. Drop the Id PK from ScheduledReports, create new PK on ReportTrackingId.
    ///   5. Drop the old Id column from ScheduledReports.
    ///   6. Create the FK: DataAcquisitionLog.ReportTrackingId ? ScheduledReports.ReportTrackingId.
    ///   7. Drop ReportStartDate / ReportEndDate columns.
    ///   8. Recreate indexes to match the new schema.
    /// </summary>
    public partial class NaturalKeyScheduledReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Backfill orphan scheduled reports from legacy logs.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'ReportStartDate') IS NOT NULL
                   AND COL_LENGTH('DataAcquisitionLog', 'ReportEndDate') IS NOT NULL
                BEGIN
                    EXEC(N'
                        INSERT INTO [ScheduledReports] ([ReportTrackingId], [Frequency], [StartDate], [EndDate], [ReportTypes], [CreateDate])
                        SELECT
                            dal.[ReportTrackingId],
                            ''Adhoc'',
                            COALESCE(dal.[ReportStartDate], ''1900-01-01''),
                            COALESCE(dal.[ReportEndDate],   ''1900-01-01''),
                            NULL,
                            GETUTCDATE()
                        FROM (
                            SELECT [ReportTrackingId], [ReportStartDate], [ReportEndDate],
                                   ROW_NUMBER() OVER (PARTITION BY [ReportTrackingId] ORDER BY [Id]) AS rn
                            FROM [DataAcquisitionLog]
                            WHERE [ReportTrackingId] IS NOT NULL
                        ) dal
                        WHERE dal.rn = 1
                          AND NOT EXISTS (
                              SELECT 1 FROM [ScheduledReports] sr
                              WHERE sr.[ReportTrackingId] = dal.[ReportTrackingId]
                          );
                    ');
                END
            ");

            // 2) Validate all non-null ReportTrackingIds are GUIDs before conversion.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM [DataAcquisitionLog]
                    WHERE [ReportTrackingId] IS NOT NULL
                      AND TRY_CONVERT(uniqueidentifier, [ReportTrackingId]) IS NULL)
                BEGIN
                    THROW 50001, 'DataAcquisitionLog contains non-GUID ReportTrackingId values. Migration aborted.', 1;
                END

                IF EXISTS (
                    SELECT 1
                    FROM [ScheduledReports]
                    WHERE [ReportTrackingId] IS NOT NULL
                      AND TRY_CONVERT(uniqueidentifier, [ReportTrackingId]) IS NULL)
                BEGIN
                    THROW 50002, 'ScheduledReports contains non-GUID ReportTrackingId values. Migration aborted.', 1;
                END

                IF EXISTS (
                    SELECT 1
                    FROM [ScheduledReports]
                    WHERE [ReportTrackingId] IS NOT NULL
                    GROUP BY TRY_CONVERT(uniqueidentifier, [ReportTrackingId])
                    HAVING COUNT(*) > 1)
                BEGIN
                    THROW 50003, 'ScheduledReports contains duplicate ReportTrackingId values after GUID normalization. Migration aborted.', 1;
                END
            ");

            // 3) Drop dependent constraints/indexes using old ScheduledReportId or ReportTrackingId(string) shape.
            migrationBuilder.Sql(@"
                DECLARE @fkName sysname;
                SELECT TOP 1 @fkName = fk.[name]
                FROM sys.foreign_keys fk
                INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
                WHERE fk.parent_object_id = OBJECT_ID('DataAcquisitionLog')
                  AND fk.referenced_object_id = OBJECT_ID('ScheduledReports')
                  AND c.[name] = 'ScheduledReportId';

                IF @fkName IS NOT NULL
                    EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @fkName + ']');

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLog_ScheduledReportId')
                    DROP INDEX [IX_DataAcquisitionLog_ScheduledReportId] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Tailing_Optimization')
                    DROP INDEX [IX_DataAcquisitionLogs_Tailing_Optimization] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted')
                    DROP INDEX [IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Paging_Default')
                    DROP INDEX [IX_DataAcquisitionLogs_Paging_Default] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_TailSent_Status')
                    DROP INDEX [IX_DataAcquisitionLogs_TailSent_Status] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_IsDeleted_Id')
                    DROP INDEX [IX_DataAcquisitionLogs_IsDeleted_Id] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('ScheduledReports') AND name = 'UX_ScheduledReports_ReportTrackingId')
                    DROP INDEX [UX_ScheduledReports_ReportTrackingId] ON [ScheduledReports];
            ");

            // 4) Remove old ScheduledReportId indirection.
            migrationBuilder.DropColumn(name: "ScheduledReportId", table: "DataAcquisitionLog");

            // 5) Promote ScheduledReports.ReportTrackingId to PK and convert to uniqueidentifier.
            migrationBuilder.Sql(@"
                ALTER TABLE [ScheduledReports] DROP CONSTRAINT [PK_ScheduledReports];
                ALTER TABLE [ScheduledReports] DROP COLUMN [Id];
                ALTER TABLE [ScheduledReports] ALTER COLUMN [ReportTrackingId] uniqueidentifier NOT NULL;
                ALTER TABLE [ScheduledReports] ADD CONSTRAINT [PK_ScheduledReports] PRIMARY KEY CLUSTERED ([ReportTrackingId]);
            ");

            // 6) Convert DataAcquisitionLog.ReportTrackingId to uniqueidentifier.
            migrationBuilder.Sql(@"
                ALTER TABLE [DataAcquisitionLog] ALTER COLUMN [ReportTrackingId] uniqueidentifier NULL;
            ");

            // 7) Add natural-key FK.
            migrationBuilder.Sql(@"
                ALTER TABLE [DataAcquisitionLog]
                    ADD CONSTRAINT [FK_DataAcquisitionLog_ScheduledReports_ReportTrackingId]
                    FOREIGN KEY ([ReportTrackingId])
                    REFERENCES [ScheduledReports] ([ReportTrackingId])
                    ON DELETE SET NULL;
            ");

            // 8) Drop legacy date columns.
            migrationBuilder.Sql(@"
                DECLARE @dfName1 sysname;
                SELECT @dfName1 = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'ReportStartDate';
                IF @dfName1 IS NOT NULL EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName1 + ']');

                DECLARE @dfName2 sysname;
                SELECT @dfName2 = dc.[name]
                FROM sys.default_constraints dc
                INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
                WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog') AND c.[name] = 'ReportEndDate';
                IF @dfName2 IS NOT NULL EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName2 + ']');
            ");

            migrationBuilder.DropColumn(name: "ReportStartDate", table: "DataAcquisitionLog");
            migrationBuilder.DropColumn(name: "ReportEndDate",   table: "DataAcquisitionLog");

            // 9) Recreate indexes in final shape.
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Paging_Default",
                table: "DataAcquisitionLog",
                columns: new[] { "ExecutionDate", "Id" },
                descending: new[] { true, true })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "IsCensus", "PatientId", "ReportableEvent",
                    "ReportTrackingId", "CorrelationId", "TraceId", "FhirVersion", "QueryType",
                    "QueryPhase", "Status", "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds"
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "Status", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "IsCensus", "PatientId", "ReportableEvent", "ReportTrackingId",
                    "CorrelationId", "FhirVersion", "QueryType", "QueryPhase", "TraceId",
                    "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds"
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] { "PatientId", "Status", "RetryAttempts", "CompletionTimeMilliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Tailing_Optimization",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "FacilityId", "ReportTrackingId", "CorrelationId", "QueryPhase" },
                filter: "[TailSent] = 0 AND [ReportTrackingId] IS NOT NULL AND [CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLog_ReportTrackingId",
                table: "DataAcquisitionLog",
                column: "ReportTrackingId");

            // Recreate IX_DataAcquisitionLogs_TailSent_Status with updated INCLUDE list
            // (removed ScheduledReportId, ReportStartDate, ReportEndDate — those columns no longer exist).
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_TailSent_Status",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "Status" })
                .Annotation("SqlServer:Include", new[] {
                    "FacilityId", "ReportTrackingId", "CorrelationId",
                    "QueryPhase", "TraceId", "PatientId", "ReportableEvent"
                });

            // Recreate IX_DataAcquisitionLogs_IsDeleted_Id (ReportTrackingId column type changed to uniqueidentifier).
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_IsDeleted_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "IsDeleted", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "PatientId", "ReportTrackingId",
                    "FhirVersion", "QueryType", "QueryPhase",
                    "ExecutionDate", "CreateDate", "RetryAttempts", "Status"
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DataAcquisitionLog_ScheduledReports_ReportTrackingId')
                    ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [FK_DataAcquisitionLog_ScheduledReports_ReportTrackingId];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLog_ReportTrackingId')
                    DROP INDEX [IX_DataAcquisitionLog_ReportTrackingId] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Tailing_Optimization')
                    DROP INDEX [IX_DataAcquisitionLogs_Tailing_Optimization] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted')
                    DROP INDEX [IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_Paging_Default')
                    DROP INDEX [IX_DataAcquisitionLogs_Paging_Default] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_TailSent_Status')
                    DROP INDEX [IX_DataAcquisitionLogs_TailSent_Status] ON [DataAcquisitionLog];

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('DataAcquisitionLog') AND name = 'IX_DataAcquisitionLogs_IsDeleted_Id')
                    DROP INDEX [IX_DataAcquisitionLogs_IsDeleted_Id] ON [DataAcquisitionLog];

                ALTER TABLE [DataAcquisitionLog] ALTER COLUMN [ReportTrackingId] nvarchar(128) NULL;

                ALTER TABLE [ScheduledReports] DROP CONSTRAINT [PK_ScheduledReports];
                ALTER TABLE [ScheduledReports] ALTER COLUMN [ReportTrackingId] nvarchar(256) NOT NULL;
                ALTER TABLE [ScheduledReports] ADD [Id] bigint IDENTITY(1,1) NOT NULL;
                ALTER TABLE [ScheduledReports] ADD CONSTRAINT [PK_ScheduledReports] PRIMARY KEY CLUSTERED ([Id]);

                CREATE UNIQUE NONCLUSTERED INDEX [UX_ScheduledReports_ReportTrackingId]
                    ON [ScheduledReports] ([ReportTrackingId]);
            ");

            // Re-add ScheduledReportId column and backfill
            migrationBuilder.AddColumn<long>(
                name: "ScheduledReportId",
                table: "DataAcquisitionLog",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE dal
                SET dal.[ScheduledReportId] = sr.[Id]
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ReportTrackingId] = sr.[ReportTrackingId]
                WHERE dal.[ReportTrackingId] IS NOT NULL;
            ");

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

            // Re-add date columns
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportStartDate", table: "DataAcquisitionLog", type: "datetime2", nullable: true);
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportEndDate",   table: "DataAcquisitionLog", type: "datetime2", nullable: true);

            migrationBuilder.Sql(@"
                UPDATE dal
                SET dal.[ReportStartDate] = sr.[StartDate],
                    dal.[ReportEndDate]   = sr.[EndDate]
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ScheduledReportId] = sr.[Id]
                WHERE dal.[ScheduledReportId] IS NOT NULL;
            ");

            // Recreate original tailing index
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Tailing_Optimization",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "FacilityId", "ReportTrackingId", "CorrelationId", "ReportStartDate", "ReportEndDate", "QueryPhase" },
                filter: "[TailSent] = 0 AND [ReportTrackingId] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [ReportStartDate] IS NOT NULL AND [ReportEndDate] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] { "PatientId", "Status", "RetryAttempts", "CompletionTimeMilliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "Status", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "IsCensus", "PatientId", "ReportableEvent", "ReportTrackingId",
                    "CorrelationId", "FhirVersion", "QueryType", "QueryPhase", "TraceId",
                    "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds"
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Paging_Default",
                table: "DataAcquisitionLog",
                columns: new[] { "ExecutionDate", "Id" },
                descending: new[] { true, true })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "IsCensus", "PatientId", "ReportableEvent",
                    "ReportTrackingId", "CorrelationId", "TraceId", "FhirVersion", "QueryType",
                    "QueryPhase", "Status", "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds"
                });

            // Recreate IX_DataAcquisitionLogs_TailSent_Status (originally created in PerformanceTuningCollapsed)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_TailSent_Status",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "Status" })
                .Annotation("SqlServer:Include", new[] {
                    "FacilityId", "ReportTrackingId", "CorrelationId",
                    "ReportStartDate", "ReportEndDate", "QueryPhase",
                    "TraceId", "PatientId", "ReportableEvent", "ScheduledReportId"
                });

            // Recreate IX_DataAcquisitionLogs_IsDeleted_Id (originally created in PerformanceTuningCollapsed)
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_IsDeleted_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "IsDeleted", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "PatientId", "ReportTrackingId",
                    "FhirVersion", "QueryType", "QueryPhase",
                    "ExecutionDate", "CreateDate", "RetryAttempts", "Status"
                });
        }
    }
}
