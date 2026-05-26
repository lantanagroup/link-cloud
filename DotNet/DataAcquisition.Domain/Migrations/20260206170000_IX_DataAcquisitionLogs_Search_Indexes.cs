using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IX_DataAcquisitionLogs_Search_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "DataAcquisitionLog",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ReportTrackingId",
                table: "DataAcquisitionLog",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] { "Priority", "PatientId", "FhirVersion", "QueryType", "QueryPhase", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "ReportTrackingId", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] { "FacilityId", "Priority", "PatientId", "FhirVersion", "QueryType", "QueryPhase", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Facility_ExecutionDate_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_ReportTrackingId_ExecutionDate_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<string>(
                name: "ReportTrackingId",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }
    }
}
