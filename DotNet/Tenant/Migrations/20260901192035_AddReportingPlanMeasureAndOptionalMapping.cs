using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingPlanMeasureAndOptionalMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans");

            migrationBuilder.AlterColumn<string>(
                name: "MeasureMappingId",
                table: "FacilityReportingPlans",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.AddColumn<string>(
                name: "Measure",
                table: "FacilityReportingPlans",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            // Backfilled from the mapping each row already points at, and before the index is
            // created rather than after: left at the empty string every existing row would carry
            // the same measure, and the new unique key would see rows that differ only by mapping
            // as duplicates of one another and refuse to build.
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.Measure = m.Measure
                FROM FacilityReportingPlans p
                INNER JOIN MeasureMappings m ON m.Id = p.MeasureMappingId;");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans",
                columns: new[] { "FacilityId", "Component", "Measure", "ReportingYear", "ReportingMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // An enrollment with no mapping is exactly what this migration made storable, and the
            // old schema requires one. There is nothing to infer - which mapping a measure belongs
            // to is the decision an admin had not made yet - so those rows go rather than being
            // given an empty foreign key that points at nothing.
            migrationBuilder.Sql("DELETE FROM FacilityReportingPlans WHERE MeasureMappingId IS NULL;");

            migrationBuilder.DropIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans");

            migrationBuilder.DropColumn(
                name: "Measure",
                table: "FacilityReportingPlans");

            migrationBuilder.AlterColumn<string>(
                name: "MeasureMappingId",
                table: "FacilityReportingPlans",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityReportingPlans_Facility_Mapping_Period",
                table: "FacilityReportingPlans",
                columns: new[] { "FacilityId", "MeasureMappingId", "ReportingMonth", "ReportingYear" },
                unique: true);
        }
    }
}
