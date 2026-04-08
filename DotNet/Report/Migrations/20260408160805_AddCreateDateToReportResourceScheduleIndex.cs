using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class AddCreateDateToReportResourceScheduleIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource");

            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource",
                columns: new[] { "FacilityId", "ReportScheduleId", "CreateDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource");

            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource",
                columns: new[] { "FacilityId", "ReportScheduleId" });
        }
    }
}
