using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class dataacquisitionlog_updates_reportDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReportEndDate",
                table: "DataAcquisitionLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReportStartDate",
                table: "DataAcquisitionLog",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportEndDate",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "ReportStartDate",
                table: "DataAcquisitionLog");
        }
    }
}
