using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class TailingMessageOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_ExecutionDate_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "DataAcquisitionLog",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Tailing_Optimization",
                table: "DataAcquisitionLog",
                columns: new[] { "TailSent", "FacilityId", "ReportTrackingId", "CorrelationId", "ReportStartDate", "ReportEndDate", "QueryPhase" },
                filter: "[TailSent] = 0 AND [ReportTrackingId] IS NOT NULL AND [CorrelationId] IS NOT NULL AND [ReportStartDate] IS NOT NULL AND [ReportEndDate] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Tailing_Optimization",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] { "FacilityId", "Priority", "PatientId", "FhirVersion", "QueryType", "QueryPhase", "Status" });
        }
    }
}
