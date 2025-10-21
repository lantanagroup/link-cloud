using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationMappingPartOfValueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "PartOfValue" },
                filter: "([PartOfId] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue",
                table: "OrganizationLocationMapping");
        }
    }
}
