using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddReportTrackingIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "IsDeleted" })
                .Annotation("SqlServer:Include", new[] { "PatientId", "Status", "RetryAttempts", "CompletionTimeMilliseconds", "ResourceAcquiredIds" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_IsDeleted",
                table: "DataAcquisitionLog");
        }
    }
}
