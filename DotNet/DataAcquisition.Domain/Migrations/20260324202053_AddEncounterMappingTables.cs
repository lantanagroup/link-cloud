using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterMappingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncounterMapping",
                columns: table => new
                {
                    EncounterMappingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EncounterId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MappedToOrg = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterMapping_EncounterMappingId", x => x.EncounterMappingId);
                });

            migrationBuilder.CreateTable(
                name: "EncounterLocation",
                columns: table => new
                {
                    EncounterLocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterMappingId = table.Column<int>(type: "int", nullable: false),
                    OrganizationLocationMappingId = table.Column<int>(type: "int", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterLocation_EncounterLocationId", x => x.EncounterLocationId);
                    table.ForeignKey(
                        name: "FK_EncounterLocation_EncounterMapping",
                        column: x => x.EncounterMappingId,
                        principalTable: "EncounterMapping",
                        principalColumn: "EncounterMappingId");
                    table.ForeignKey(
                        name: "FK_EncounterLocation_OrganizationLocationMapping",
                        column: x => x.OrganizationLocationMappingId,
                        principalTable: "OrganizationLocationMapping",
                        principalColumn: "LocationMappingId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLocation_EncounterMappingId",
                table: "EncounterLocation",
                column: "EncounterMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLocation_OrganizationLocationMappingId",
                table: "EncounterLocation",
                column: "OrganizationLocationMappingId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterMapping_FacilityId_EncounterId",
                table: "EncounterMapping",
                columns: new[] { "FacilityId", "EncounterId" });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterMapping_FacilityId_PatientId",
                table: "EncounterMapping",
                columns: new[] { "FacilityId", "PatientId" });

            migrationBuilder.CreateIndex(
                name: "UQ_EncounterMapping_FacilityId_EncounterId",
                table: "EncounterMapping",
                columns: new[] { "FacilityId", "EncounterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterLocation");

            migrationBuilder.DropTable(
                name: "EncounterMapping");
        }
    }
}
