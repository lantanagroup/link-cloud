using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class DateTimeOffsetScheduleDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_Facility_Period",
                table: "ReportSchedule");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReportStartDate",
                table: "ReportSchedule",
                type: "datetimeoffset(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "ReportEndDate",
                table: "ReportSchedule",
                type: "datetimeoffset(7)",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Facility_Period",
                table: "ReportSchedule",
                columns: new[] { "FacilityId", "ReportStartDate", "ReportEndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportSchedules_Facility_Period",
                table: "ReportSchedule");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ReportStartDate",
                table: "ReportSchedule",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ReportEndDate",
                table: "ReportSchedule",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset(7)");

            migrationBuilder.CreateIndex(
                name: "IX_ReportSchedules_Facility_Period",
                table: "ReportSchedule",
                columns: new[] { "FacilityId", "ReportStartDate", "ReportEndDate" });
        }
    }
}
