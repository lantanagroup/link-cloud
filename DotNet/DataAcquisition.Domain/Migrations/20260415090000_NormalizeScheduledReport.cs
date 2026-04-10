using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Normalises the ScheduledReport JSON column on DataAcquisitionLog into a
    /// dedicated ScheduledReports table with a FK.  Each unique ReportTrackingId
    /// gets a single row; all logs sharing that report point to it.
    /// </summary>
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260415090000_NormalizeScheduledReport")]
    public partial class NormalizeScheduledReport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the new table
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

            // 2. Add the FK column to DataAcquisitionLog
            migrationBuilder.AddColumn<long>(
                name: "ScheduledReportId",
                table: "DataAcquisitionLog",
                type: "bigint",
                nullable: true);

            // 3. Populate ScheduledReports from the existing JSON column.
            //    Parse the JSON to extract Frequency, StartDate, EndDate, ReportTypes.
            //    Group by ReportTrackingId to de-duplicate.
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
                        [ReportTrackingId],
                        [ScheduledReport],
                        [ReportStartDate],
                        [ReportEndDate],
                        ROW_NUMBER() OVER (PARTITION BY [ReportTrackingId] ORDER BY [Id]) AS rn
                    FROM [DataAcquisitionLog]
                    WHERE [ScheduledReport] IS NOT NULL
                      AND [ReportTrackingId] IS NOT NULL
                ) dal
                WHERE dal.rn = 1;
            ");

            // 4. Link existing logs to their ScheduledReports row
            migrationBuilder.Sql(@"
                UPDATE dal
                SET dal.[ScheduledReportId] = sr.[Id]
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ReportTrackingId] = sr.[ReportTrackingId]
                WHERE dal.[ScheduledReport] IS NOT NULL;
            ");

            // 5. Drop the old JSON column and rebuild index that included it.
            //    The index IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id
            //    had ScheduledReport in its INCLUDE list.
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

            migrationBuilder.DropColumn(
                name: "ScheduledReport",
                table: "DataAcquisitionLog");

            // Recreate index without ScheduledReport in INCLUDE
            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id]
                ON [DataAcquisitionLog] ([FacilityId], [Status], [ExecutionDate], [Id])
                INCLUDE (
                    [Priority], [IsCensus], [PatientId], [ReportableEvent],
                    [ReportTrackingId], [CorrelationId], [FhirVersion], [QueryType],
                    [QueryPhase], [TraceId], [RetryAttempts], [CompletionDate],
                    [CompletionTimeMilliseconds], [ResourceAcquiredIds], [Notes]
                );

                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_TailSent_Status]
                ON [DataAcquisitionLog] ([TailSent], [Status])
                INCLUDE (
                    [FacilityId], [ReportTrackingId], [CorrelationId],
                    [ReportStartDate], [ReportEndDate], [QueryPhase],
                    [TraceId], [PatientId], [ReportableEvent], [ScheduledReportId]
                );
            ");

            // 6. Add FK constraint
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
            // Drop FK and index
            migrationBuilder.DropForeignKey(
                name: "FK_DataAcquisitionLog_ScheduledReports_ScheduledReportId",
                table: "DataAcquisitionLog");

            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLog_ScheduledReportId",
                table: "DataAcquisitionLog");

            // Re-add ScheduledReport JSON column
            migrationBuilder.AddColumn<string>(
                name: "ScheduledReport",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            // Restore JSON from ScheduledReports table
            migrationBuilder.Sql(@"
                UPDATE dal
                SET dal.[ScheduledReport] = CONCAT(
                    '{',
                    '""ReportTrackingId"":""', STRING_ESCAPE(sr.[ReportTrackingId], 'json'), '""',
                    ',""Frequency"":""', STRING_ESCAPE(sr.[Frequency], 'json'), '""',
                    ',""StartDate"":""', CONVERT(nvarchar(30), sr.[StartDate], 127), '""',
                    ',""EndDate"":""', CONVERT(nvarchar(30), sr.[EndDate], 127), '""',
                    ',""ReportTypes"":[',
                        ISNULL((
                            SELECT STRING_AGG(CONCAT('""', STRING_ESCAPE(LTRIM(RTRIM(value)), 'json'), '""'), ',')
                            FROM STRING_SPLIT(sr.[ReportTypes], ',')
                        ), ''),
                    ']',
                    '}'
                )
                FROM [DataAcquisitionLog] dal
                INNER JOIN [ScheduledReports] sr ON dal.[ScheduledReportId] = sr.[Id];
            ");

            // Rebuild index with ScheduledReport in INCLUDE
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

                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id]
                ON [DataAcquisitionLog] ([FacilityId], [Status], [ExecutionDate], [Id])
                INCLUDE (
                    [Priority], [IsCensus], [PatientId], [ReportableEvent],
                    [ReportTrackingId], [CorrelationId], [FhirVersion], [QueryType],
                    [QueryPhase], [TraceId], [RetryAttempts], [CompletionDate],
                    [CompletionTimeMilliseconds], [ResourceAcquiredIds], [Notes],
                    [ScheduledReport]
                );

                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_TailSent_Status]
                ON [DataAcquisitionLog] ([TailSent], [Status])
                INCLUDE (
                    [FacilityId], [ReportTrackingId], [CorrelationId],
                    [ReportStartDate], [ReportEndDate], [QueryPhase],
                    [TraceId], [PatientId], [ReportableEvent], [ScheduledReport]
                );
            ");

            // Drop FK column and ScheduledReports table
            migrationBuilder.DropColumn(
                name: "ScheduledReportId",
                table: "DataAcquisitionLog");

            migrationBuilder.DropTable(
                name: "ScheduledReports");
        }
    }
}
