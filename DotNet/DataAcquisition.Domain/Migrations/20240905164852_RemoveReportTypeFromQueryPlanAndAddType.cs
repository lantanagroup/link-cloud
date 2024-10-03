using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RemoveReportTypeFromQueryPlanAndAddType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "queryPlan");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "queryPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "queryPlan");

            migrationBuilder.AddColumn<string>(
                name: "ReportType",
                table: "queryPlan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
