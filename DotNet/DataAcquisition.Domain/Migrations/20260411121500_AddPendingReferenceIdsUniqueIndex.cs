using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingReferenceIdsUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_PendingReferenceIds_FhirQueryId_ResourceType_ResourceId",
                table: "PendingReferenceIds",
                columns: new[] { "FhirQueryId", "ResourceType", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PendingReferenceIds_FhirQueryId_ResourceType_ResourceId",
                table: "PendingReferenceIds");
        }
    }
}
