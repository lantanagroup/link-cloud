using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Adds two indexes to eliminate full table scans during the most frequent query paths:
    ///
    /// 1. IX_DataAcquisitionLogs_IsDeleted_Id — Covers the default pagination query
    ///    (SearchQueryLogSummaryAsync) when no facility/report filter is provided.
    ///    The UI's default view is "all non-deleted logs, ordered by Id DESC, page N".
    ///    Without this index, SQL Server does a full clustered index scan + sort on every page load.
    ///
    /// 2. IX_DataAcquisitionLogs_TailSent_Status — Covers the GetTailingMessages query
    ///    which filters on TailSent = 0 AND Status IN (terminal statuses).
    ///    Includes the GROUP BY columns so the query can be satisfied from the index alone.
    /// </summary>
    public partial class IX_DataAcquisitionLogs_DefaultPaging_And_Tailing : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default pagination: WHERE IsDeleted = 0 ORDER BY Id DESC
            // INCLUDE covers the SELECT list of SearchQueryLogSummaryAsync
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_IsDeleted_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "IsDeleted", "Id" })
                .Annotation("SqlServer:Include", new[] {
                    "Priority", "FacilityId", "PatientId", "ReportTrackingId",
                    "FhirVersion", "QueryType", "QueryPhase",
                    "ExecutionDate", "CreateDate", "RetryAttempts", "Status"
                });

            // Tailing query: WHERE TailSent = 0 AND Status IN (...) AND ReportTrackingId IS NOT NULL ...
            // GROUP BY FacilityId, ReportTrackingId, CorrelationId, ReportStartDate, ReportEndDate, QueryPhase
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_TailSent_Status",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "Status" })
                .Annotation("SqlServer:Include", new[] {
                    "FacilityId", "ReportTrackingId", "CorrelationId",
                    "ReportStartDate", "ReportEndDate", "QueryPhase",
                    "TraceId", "PatientId", "ReportableEvent", "ScheduledReport"
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_IsDeleted_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_TailSent_Status",
                table: "DataAcquisitionLog");
        }
    }
}
