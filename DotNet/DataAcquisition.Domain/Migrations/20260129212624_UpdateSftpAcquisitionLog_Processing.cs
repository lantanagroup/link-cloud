using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSftpAcquisitionLog_Processing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncounterCount",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "SftpAcquisitionLog");

            migrationBuilder.RenameColumn(
                name: "PatientCount",
                table: "SftpAcquisitionLog",
                newName: "AcquisitionType");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessDate",
                table: "SftpAcquisitionLog",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AddColumn<string>(
                name: "FileNames",
                table: "SftpAcquisitionLog",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "SftpAcquisitionLog",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OriginatingSpanId",
                table: "SftpAcquisitionLog",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginatingTraceId",
                table: "SftpAcquisitionLog",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryAttempts",
                table: "SftpAcquisitionLog",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileNames",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "OriginatingSpanId",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "OriginatingTraceId",
                table: "SftpAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "RetryAttempts",
                table: "SftpAcquisitionLog");

            migrationBuilder.RenameColumn(
                name: "AcquisitionType",
                table: "SftpAcquisitionLog",
                newName: "PatientCount");

            migrationBuilder.AlterColumn<DateTime>(
                name: "ProcessDate",
                table: "SftpAcquisitionLog",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EncounterCount",
                table: "SftpAcquisitionLog",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "SftpAcquisitionLog",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }
    }
}
