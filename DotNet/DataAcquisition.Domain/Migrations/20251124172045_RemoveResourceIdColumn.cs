using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RemoveResourceIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_DataAcquisitionLogs_Paging_Default", table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                 name: "ResourceId",
                 table: "DataAcquisitionLog");

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Paging_Default",
                table: "DataAcquisitionLog",
                columns: new[] { "ExecutionDate", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "Priority", "FacilityId", "IsCensus", "PatientId", "ReportableEvent", "ReportTrackingId", "CorrelationId", "TraceId", "FhirVersion", "QueryType", "QueryPhase", "Status", "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ResourceId",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Paging_Default",
                table: "DataAcquisitionLog",
                columns: new[] { "ExecutionDate", "Id" },
                descending: new bool[0])
                .Annotation("SqlServer:Include", new[] { "Priority", "FacilityId", "IsCensus", "PatientId", "ReportableEvent", "ReportTrackingId", "CorrelationId", "TraceId", "FhirVersion", "QueryType", "QueryPhase", "Status", "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds, ResourceId" });
        }
    }
}
