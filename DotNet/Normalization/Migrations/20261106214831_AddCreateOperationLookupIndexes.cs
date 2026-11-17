using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class AddCreateOperationLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorVersionOperationPreset_OperationResourceTypeId",
                table: "VendorVersionOperationPreset");

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersionOperationPreset_OperationResourceTypeId_VendorVersionId",
                table: "VendorVersionOperationPreset",
                columns: new[] { "OperationResourceTypeId", "VendorVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Operation_FacilityId_OperationType",
                table: "Operation",
                columns: new[] { "FacilityId", "OperationType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VendorVersionOperationPreset_OperationResourceTypeId_VendorVersionId",
                table: "VendorVersionOperationPreset");

            migrationBuilder.DropIndex(
                name: "IX_Operation_FacilityId_OperationType",
                table: "Operation");

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersionOperationPreset_OperationResourceTypeId",
                table: "VendorVersionOperationPreset",
                column: "OperationResourceTypeId");
        }
    }
}
