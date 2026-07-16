using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationConfigurationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrganizationLocationConfiguration",
                columns: table => new
                {
                    ConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FacilityId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getutcdate())"),
                    ModifiedOn = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationConfiguration_ConfigId", x => x.ConfigId);
                });

            migrationBuilder.CreateTable(
                name: "OrganizationLocationCondition",
                columns: table => new
                {
                    ConditionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfigId = table.Column<int>(type: "int", nullable: false),
                    FhirPath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedOn = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getutcdate())"),
                    ModifiedOn = table.Column<DateTime>(type: "datetime", nullable: true, defaultValueSql: "(getutcdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationCondition_ConditionId", x => x.ConditionId);
                    table.ForeignKey(
                        name: "FK_LocationCondition_ConfigId",
                        column: x => x.ConfigId,
                        principalTable: "OrganizationLocationConfiguration",
                        principalColumn: "ConfigId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocationConditions_ConfigId",
                table: "OrganizationLocationCondition",
                column: "ConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationConfigurations_FacilityId",
                table: "OrganizationLocationConfiguration",
                column: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "OrganizationLocationCondition");
            migrationBuilder.DropTable(name: "OrganizationLocationConfiguration");
        }
    }
}