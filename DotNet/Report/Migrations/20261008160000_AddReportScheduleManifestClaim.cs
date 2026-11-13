using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class AddReportScheduleManifestClaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ManifestClaimedAt",
                table: "ReportSchedule",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ManifestState",
                table: "ReportSchedule",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ManifestClaimedAt",
                table: "ReportSchedule");

            migrationBuilder.DropColumn(
                name: "ManifestState",
                table: "ReportSchedule");
        }
    }
}
