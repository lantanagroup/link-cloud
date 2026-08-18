using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationMappingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "OrganizationLocationConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "OrganizationLocationCondition",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true,
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "FhirPath",
                table: "OrganizationLocationCondition",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.CreateTable(
                name: "OrganizationLocationMapping",
                columns: table => new
                {
                    LocationMappingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    LocationId = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LocationName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LocationAlias = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PartOfValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PartOfId = table.Column<int>(type: "int", nullable: true),
                    IsOrgLocation = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getutcdate())"),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationLocationMapping_LocationMappingId", x => x.LocationMappingId);
                    table.ForeignKey(
                        name: "FK_LocationMapping_PartOf",
                        column: x => x.PartOfId,
                        principalTable: "OrganizationLocationMapping",
                        principalColumn: "LocationMappingId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_FacilityId_LocationId",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_FacilityId_LocationId_IsOrgLocation",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "LocationId", "IsOrgLocation" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_PartOfId",
                table: "OrganizationLocationMapping",
                column: "PartOfId",
                filter: "([PartOfId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "UQ_LocationMapping_Facility_Location",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "LocationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganizationLocationMapping");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "OrganizationLocationConfiguration",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<int>(
                name: "Priority",
                table: "OrganizationLocationCondition",
                type: "int",
                nullable: true,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<string>(
                name: "FhirPath",
                table: "OrganizationLocationCondition",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
