using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeDataAcquisitionLogIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Facility_ExecutionDate_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DataAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryType",
                table: "DataAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryPhase",
                table: "DataAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "DataAcquisitionLog",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "Status", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] { "Priority", "IsCensus", "PatientId", "ReportableEvent", "ReportTrackingId", "CorrelationId", "FhirVersion", "QueryType", "QueryPhase", "TraceId", "RetryAttempts", "CompletionDate", "CompletionTimeMilliseconds", "ResourceAcquiredIds", "Notes", "ScheduledReport" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Facility_Status_ExecutionDate_Id",
                table: "DataAcquisitionLog");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryType",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryPhase",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Facility_ExecutionDate_Id",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "ExecutionDate", "Id" })
                .Annotation("SqlServer:Include", new[] { "Priority", "PatientId", "FhirVersion", "QueryType", "QueryPhase", "Status" });
        }
    }
}
