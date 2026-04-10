using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Moves DataAcquisitionLog.Notes out of the wide log row into a child table.
    /// </summary>
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260415100000_NormalizeDataAcquisitionLogNotes")]
    public partial class NormalizeDataAcquisitionLogNotes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            // Migrate existing Notes JSON array into child rows.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    INSERT INTO [DataAcquisitionLogNotes] ([DataAcquisitionLogId], [Note], [CreateDate])
                    SELECT
                        dal.[Id],
                        CAST(j.[value] AS nvarchar(max)),
                        GETUTCDATE()
                    FROM [DataAcquisitionLog] dal
                    CROSS APPLY OPENJSON(dal.[Notes]) j
                    WHERE dal.[Notes] IS NOT NULL
                      AND ISJSON(dal.[Notes]) = 1;
                END
            ");

            // Rebuild index that previously included [Notes].
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('DataAcquisitionLog')
                      AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                BEGIN
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];
                END
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('DataAcquisitionLog', 'Notes') IS NOT NULL
                BEGIN
                    DECLARE @dfName sysname;

                    SELECT @dfName = dc.[name]
                    FROM sys.default_constraints dc
                    INNER JOIN sys.columns c
                        ON dc.parent_object_id = c.object_id
                       AND dc.parent_column_id = c.column_id
                    WHERE dc.parent_object_id = OBJECT_ID('DataAcquisitionLog')
                      AND c.[name] = 'Notes';

                    IF @dfName IS NOT NULL
                    BEGIN
                        EXEC('ALTER TABLE [DataAcquisitionLog] DROP CONSTRAINT [' + @dfName + ']');
                    END

                    ALTER TABLE [DataAcquisitionLog] DROP COLUMN [Notes];
                END
            ");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id]
                ON [DataAcquisitionLog] ([FacilityId], [Status], [ExecutionDate], [Id])
                INCLUDE (
                    [Priority], [IsCensus], [PatientId], [ReportableEvent],
                    [ReportTrackingId], [CorrelationId], [FhirVersion], [QueryType],
                    [QueryPhase], [TraceId], [RetryAttempts], [CompletionDate],
                    [CompletionTimeMilliseconds], [ResourceAcquiredIds]
                );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            // Restore Notes JSON array from child rows.
            migrationBuilder.Sql(@"
                UPDATE dal
                SET dal.[Notes] = COALESCE(
                    (
                        SELECT
                            '[' + STRING_AGG('""' + STRING_ESCAPE(n.[Note], 'json') + '""', ',') + ']'
                        FROM [DataAcquisitionLogNotes] n
                        WHERE n.[DataAcquisitionLogId] = dal.[Id]
                    ),
                    '[]'
                )
                FROM [DataAcquisitionLog] dal;
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('DataAcquisitionLog')
                      AND name = 'IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id')
                BEGIN
                    DROP INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id] ON [DataAcquisitionLog];
                END
            ");

            migrationBuilder.Sql(@"
                CREATE NONCLUSTERED INDEX [IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id]
                ON [DataAcquisitionLog] ([FacilityId], [Status], [ExecutionDate], [Id])
                INCLUDE (
                    [Priority], [IsCensus], [PatientId], [ReportableEvent],
                    [ReportTrackingId], [CorrelationId], [FhirVersion], [QueryType],
                    [QueryPhase], [TraceId], [RetryAttempts], [CompletionDate],
                    [CompletionTimeMilliseconds], [ResourceAcquiredIds], [Notes]
                );
            ");

            migrationBuilder.DropTable(
                name: "DataAcquisitionLogNotes");
        }
    }
}
