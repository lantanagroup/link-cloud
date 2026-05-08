using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Census.Migrations
{
    /// <inheritdoc />
    public partial class ComputedColumnPatientHistoryReportId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReportId",
                table: "PatientCensusHistory",
                type: "nvarchar(max)",
                nullable: false,
                computedColumnSql: "CONCAT(FacilityId, '-', CensusDateTime)",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ReportId",
                table: "PatientCensusHistory",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldComputedColumnSql: "CONCAT(FacilityId, '-', CensusDateTime)");
        }
    }
}
