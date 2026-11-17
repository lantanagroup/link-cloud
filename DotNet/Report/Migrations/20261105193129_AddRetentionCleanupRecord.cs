using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionCleanupRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RetentionStatus",
                table: "ReportSchedule",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.CreateTable(
                name: "RetentionCleanup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatedBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    InitiatedDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    InitiatedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InitiatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CutoffDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionCleanup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RetentionCleanupParticipant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionCleanupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionCleanupParticipant", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionCleanupParticipant_RetentionCleanup",
                        column: x => x.RetentionCleanupId,
                        principalTable: "RetentionCleanup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetentionCleanupReport",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionCleanupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportScheduleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RetentionWindowUnit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    RetentionWindowCount = table.Column<int>(type: "int", nullable: true),
                    CutoffDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionCleanupReport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionCleanupReport_RetentionCleanup",
                        column: x => x.RetentionCleanupId,
                        principalTable: "RetentionCleanup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RetentionCleanupResult",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionCleanupParticipantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RetentionCleanupReportId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecordsAffected = table.Column<long>(type: "bigint", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSentDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SendCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionCleanupResult", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionCleanupResult_RetentionCleanupParticipant",
                        column: x => x.RetentionCleanupParticipantId,
                        principalTable: "RetentionCleanupParticipant",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RetentionCleanupResult_RetentionCleanupReport",
                        column: x => x.RetentionCleanupReportId,
                        principalTable: "RetentionCleanupReport",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanup_InitiatedDateTime",
                table: "RetentionCleanup",
                column: "InitiatedDateTime",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanup_RunId",
                table: "RetentionCleanup",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanup_Status",
                table: "RetentionCleanup",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanupParticipant_Cleanup_Service",
                table: "RetentionCleanupParticipant",
                columns: new[] { "RetentionCleanupId", "ServiceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanupReport_Cleanup_Report",
                table: "RetentionCleanupReport",
                columns: new[] { "RetentionCleanupId", "ReportScheduleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanupReport_ReportScheduleId",
                table: "RetentionCleanupReport",
                column: "ReportScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanupResult_Participant_Report",
                table: "RetentionCleanupResult",
                columns: new[] { "RetentionCleanupParticipantId", "RetentionCleanupReportId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionCleanupResult_RetentionCleanupReportId",
                table: "RetentionCleanupResult",
                column: "RetentionCleanupReportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RetentionCleanupResult");

            migrationBuilder.DropTable(
                name: "RetentionCleanupParticipant");

            migrationBuilder.DropTable(
                name: "RetentionCleanupReport");

            migrationBuilder.DropTable(
                name: "RetentionCleanup");

            migrationBuilder.DropColumn(
                name: "RetentionStatus",
                table: "ReportSchedule");
        }
    }
}
