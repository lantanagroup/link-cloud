using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class IX_Report_PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource",
                columns: new[] { "FacilityId", "ReportScheduleId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Schedule_Patient",
                table: "ReportEntry",
                columns: new[] { "ReportScheduleId", "PatientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource");

            migrationBuilder.DropIndex(
                name: "IX_ReportEntries_Schedule_Patient",
                table: "ReportEntry");
        }
    }
}
