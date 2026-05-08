using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class MultiMeasureChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MRPCreatedDate",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MRPModifyDate",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MonthlyReportingPlans",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "ScheduledTasks",
                table: "Facilities");

            migrationBuilder.AddColumn<string>(
                name: "ScheduledReports",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScheduledReports",
                table: "Facilities");

            migrationBuilder.AddColumn<DateTime>(
                name: "MRPCreatedDate",
                table: "Facilities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MRPModifyDate",
                table: "Facilities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonthlyReportingPlans",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScheduledTasks",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
