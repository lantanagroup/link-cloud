using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSftpBenchmarkingAndAcquisitionConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcquisitionConfigurations",
                table: "SftpConfiguration",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EnableBenchmarking",
                table: "SftpConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "AcquisitionType",
                table: "SftpAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Benchmarks",
                table: "SftpAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledDate",
                table: "SftpAcquisitionLog",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SftpAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_SftpAcquisitionLog_ScheduledDate",
                table: "SftpAcquisitionLog",
                column: "ScheduledDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SftpAcquisitionLog_ScheduledDate",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "AcquisitionConfigurations",
                table: "SftpConfiguration");

            migrationBuilder.DropColumn(
                name: "EnableBenchmarking",
                table: "SftpConfiguration");

            migrationBuilder.DropColumn(
                name: "Benchmarks",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SftpAcquisitionLog");

            migrationBuilder.AlterColumn<int>(
                name: "AcquisitionType",
                table: "SftpAcquisitionLog",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
