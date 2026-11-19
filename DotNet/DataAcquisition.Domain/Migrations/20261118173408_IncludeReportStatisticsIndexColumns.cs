using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IncludeReportStatisticsIndexColumns : Migration
    {
        private const string LogTable = "DataAcquisitionLog";
        private const string ReportTrackingIndex = "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted";
        private const string ReportTrackingKey = "[ReportTrackingId], [IsDeleted]";
        private const string ReportTrackingIncludeBefore =
            "[PatientId], [Status], [RetryAttempts], [CompletionTimeMilliseconds]";
        private const string ReportTrackingIncludeAfter = ReportTrackingIncludeBefore + ", [QueryType], [QueryPhase]";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RebuildIndexSql(ReportTrackingIndex,
                                                 LogTable,
                                                 ReportTrackingKey,
                                                 ReportTrackingIncludeAfter));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RebuildIndexSql(ReportTrackingIndex,
                                                 LogTable,
                                                 ReportTrackingKey,
                                                 ReportTrackingIncludeBefore));
        }

        // DROP_EXISTING rebuilds in place, so the index never disappears while a large table is re-indexed.
        // ONLINE keeps the table writable during the rebuild, but only Enterprise/Developer (3), Azure SQL
        // Database (5) and Managed Instance (8) support it, so other editions rebuild offline.
        private static string RebuildIndexSql(string indexName, string table, string keyColumns, string includeColumns)
        {
            return $"""
                    DECLARE @online nvarchar(3) =
                        CASE WHEN CAST(SERVERPROPERTY('EngineEdition') AS int) IN (3, 5, 8) THEN N'ON' ELSE N'OFF' END;
                    EXEC (N'CREATE NONCLUSTERED INDEX [{indexName}] ON [{table}] ({keyColumns}) ' +
                          N'INCLUDE ({includeColumns}) WITH (DROP_EXISTING = ON, ONLINE = ' + @online + N');');
                    """;
        }
    }
}
