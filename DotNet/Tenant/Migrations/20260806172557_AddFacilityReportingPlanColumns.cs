using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityReportingPlanColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilityId",
                table: "FacilityReportingPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsReporting",
                table: "FacilityReportingPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MeasureMappingId",
                table: "FacilityReportingPlans",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ReportingMonth",
                table: "FacilityReportingPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReportingYear",
                table: "FacilityReportingPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Rows written before these columns existed carry only an Id: the scaffolded API accepted a
            // reporting plan that identified no facility, measure or period. They take the defaults
            // above, which no measure mapping matches, so the foreign key below could not be created
            // and two such rows would collide on the unique index. They describe nothing, so they are
            // dropped rather than migrated.
            migrationBuilder.Sql(
                "DELETE FROM FacilityReportingPlans WHERE MeasureMappingId NOT IN (SELECT Id FROM MeasureMappings)");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans",
                columns: new[] { "FacilityId", "MeasureMappingId", "ReportingMonth", "ReportingYear" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReportingPlans_Facility_Period",
                table: "FacilityReportingPlans",
                columns: new[] { "FacilityId", "ReportingYear", "ReportingMonth" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReportingPlans_MeasureMappingId",
                table: "FacilityReportingPlans",
                column: "MeasureMappingId");

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityReportingPlans_MeasureMappings_MeasureMappingId",
                table: "FacilityReportingPlans",
                column: "MeasureMappingId",
                principalTable: "MeasureMappings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FacilityReportingPlans_MeasureMappings_MeasureMappingId",
                table: "FacilityReportingPlans");

            migrationBuilder.DropIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans");

            migrationBuilder.DropIndex(
                name: "IX_FacilityReportingPlans_Facility_Period",
                table: "FacilityReportingPlans");

            migrationBuilder.DropIndex(
                name: "IX_FacilityReportingPlans_MeasureMappingId",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "IsReporting",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "MeasureMappingId",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "ReportingMonth",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "ReportingYear",
                table: "FacilityReportingPlans");
        }
    }
}
