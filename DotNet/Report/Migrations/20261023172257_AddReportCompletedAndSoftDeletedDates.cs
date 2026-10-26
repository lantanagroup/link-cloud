using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class AddReportCompletedAndSoftDeletedDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedDate",
                table: "ReportSchedule",
                type: "datetimeoffset(7)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SoftDeletedDate",
                table: "ReportSchedule",
                type: "datetimeoffset(7)",
                nullable: true);

            // Backfill per the approved retention eligibility rule (LEGLINK-1243, ADR 08 workflow step 1).
            // The source columns are datetime2 in UTC, which SQL Server converts to datetimeoffset at +00:00.
            // See dev-docs/report-retention-dates.md.
            migrationBuilder.Sql(
                "UPDATE [ReportSchedule] " +
                "SET [CompletedDate] = COALESCE([SubmitReportDateTime], [ModifyDate], [CreateDate]) " +
                "WHERE [Status] = 'Submitted';");

            migrationBuilder.Sql(
                "UPDATE [ReportSchedule] " +
                "SET [CompletedDate] = COALESCE([ModifyDate], [CreateDate]) " +
                "WHERE [Status] = 'CompletedNotSubmitted';");

            migrationBuilder.Sql(
                "UPDATE [ReportSchedule] " +
                "SET [SoftDeletedDate] = COALESCE([ModifyDate], [CreateDate]) " +
                "WHERE [IsDeleted] = 1 AND [Status] NOT IN ('Submitted', 'CompletedNotSubmitted');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "ReportSchedule");

            migrationBuilder.DropColumn(
                name: "SoftDeletedDate",
                table: "ReportSchedule");
        }
    }
}
