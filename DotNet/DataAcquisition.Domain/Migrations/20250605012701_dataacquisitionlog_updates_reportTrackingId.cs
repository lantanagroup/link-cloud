using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class dataacquisitionlog_updates_reportTrackingId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "MaxConcurrentRequests",
                table: "fhirQueryConfiguration",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ReportTrackingId",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportTrackingId",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<int>(
                name: "MaxConcurrentRequests",
                table: "fhirQueryConfiguration",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
