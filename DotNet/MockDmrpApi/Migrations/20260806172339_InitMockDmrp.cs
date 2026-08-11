using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.MockDmrpApi.Migrations
{
    /// <inheritdoc />
    public partial class InitMockDmrp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MockDmrpEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Component = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Measure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReportingMonth = table.Column<int>(type: "int", nullable: true),
                    ReportingYear = table.Column<int>(type: "int", nullable: false),
                    IsReporting = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MockDmrpEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UX_MockDmrpEntries_Facility_Component_Period_Measure",
                table: "MockDmrpEntries",
                columns: new[] { "FacilityId", "Component", "ReportingYear", "ReportingMonth", "Measure" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MockDmrpEntries");
        }
    }
}
