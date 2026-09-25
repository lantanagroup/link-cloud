using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMrnIntakeAndCommit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MrnIntakeRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IntakeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MrnIntakeRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OnboardingCommits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingCommits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MrnIntakeRecords_FacilityId",
                table: "MrnIntakeRecords",
                column: "FacilityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingCommits_FacilityId",
                table: "OnboardingCommits",
                column: "FacilityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MrnIntakeRecords");

            migrationBuilder.DropTable(
                name: "OnboardingCommits");
        }
    }
}
