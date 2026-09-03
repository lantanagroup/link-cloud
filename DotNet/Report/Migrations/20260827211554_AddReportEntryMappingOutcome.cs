using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class AddReportEntryMappingOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReportEntryMappingOutcome",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocationOrgStatus = table.Column<int>(type: "int", nullable: false),
                    EncounterMappingStatus = table.Column<int>(type: "int", nullable: false),
                    AcquisitionDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcquisitionEvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HslocMappingStatus = table.Column<int>(type: "int", nullable: false),
                    NormalizationDetails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NormalizationEvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportEntryMappingOutcome", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportEntryMappingOutcome_ReportSchedule_ReportScheduleId",
                        column: x => x.ReportScheduleId,
                        principalTable: "ReportSchedule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntryMappingOutcomes_Facility_Patient",
                table: "ReportEntryMappingOutcome",
                columns: new[] { "FacilityId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntryMappingOutcomes_Schedule_Patient",
                table: "ReportEntryMappingOutcome",
                columns: new[] { "ReportScheduleId", "PatientId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportEntryMappingOutcome");
        }
    }
}
