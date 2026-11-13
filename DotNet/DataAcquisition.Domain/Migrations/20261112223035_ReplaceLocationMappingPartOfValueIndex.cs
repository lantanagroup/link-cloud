using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceLocationMappingPartOfValueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the replacement before dropping the old index so the column pair is never unindexed.
            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue_All",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "PartOfValue" })
                .Annotation("SqlServer:Include", new[] { "PartOfId", "IsOrgLocation" });

            migrationBuilder.DropIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue",
                table: "OrganizationLocationMapping");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue",
                table: "OrganizationLocationMapping",
                columns: new[] { "FacilityId", "PartOfValue" },
                filter: "([PartOfId] IS NULL)");

            migrationBuilder.DropIndex(
                name: "IX_LocationMapping_FacilityId_PartOfValue_All",
                table: "OrganizationLocationMapping");
        }
    }
}
