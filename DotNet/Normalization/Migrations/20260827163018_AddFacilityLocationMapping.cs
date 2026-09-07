using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityLocationMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacilityLocations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false, defaultValueSql: "(newid())"),
                    FacilityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LocationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartOfId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentFacilityLocationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LocationName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LocationAlias = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityLocation_ParentFacilityLocation",
                        column: x => x.ParentFacilityLocationId,
                        principalTable: "FacilityLocations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FacilityLocationLocalCodeMappings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false, defaultValueSql: "(newid())"),
                    FacilityLocationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LocalCodeSystem = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LocalCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HSLOCId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityLocationLocalCodeMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityLocationLocalCodeMapping_FacilityLocation",
                        column: x => x.FacilityLocationId,
                        principalTable: "FacilityLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityLocationLocalCodeMapping_HSLOC",
                        column: x => x.HSLOCId,
                        principalTable: "HSLOC",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityLocationLocalCodeMappings_FacilityLocationId_LocalCodeSystem_LocalCode",
                table: "FacilityLocationLocalCodeMappings",
                columns: new[] { "FacilityLocationId", "LocalCodeSystem", "LocalCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityLocationLocalCodeMappings_HSLOCId",
                table: "FacilityLocationLocalCodeMappings",
                column: "HSLOCId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityLocations_FacilityId_LocationId",
                table: "FacilityLocations",
                columns: new[] { "FacilityId", "LocationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityLocations_ParentFacilityLocationId",
                table: "FacilityLocations",
                column: "ParentFacilityLocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityLocationLocalCodeMappings");

            migrationBuilder.DropTable(
                name: "FacilityLocations");
        }
    }
}
