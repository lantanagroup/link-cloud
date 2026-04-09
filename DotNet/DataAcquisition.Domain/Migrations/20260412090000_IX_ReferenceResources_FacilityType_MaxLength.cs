using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Adds max-length constraints to ReferenceResources columns (FacilityId, ResourceId, ResourceType)
    /// so they can be indexed, then creates a composite covering index for the SearchAsync lookup pattern.
    ///
    /// Previously these columns were nvarchar(max), which prevented SQL Server from creating B-tree
    /// indexes. Under multi-patient test load, every HandleReferenceResourceBatch call caused a full
    /// table scan, contributing to the cascading SQL timeout storm.
    /// </summary>
    public partial class IX_ReferenceResources_FacilityType_MaxLength : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Alter columns from nvarchar(max) to bounded lengths.
            // Existing data should fit easily (FacilityId ~50 chars, ResourceId ~100 chars, ResourceType ~30 chars).
            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "ReferenceResources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceId",
                table: "ReferenceResources",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceType",
                table: "ReferenceResources",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Composite index covering the SearchAsync WHERE clause:
            //   WHERE FacilityId = @fac AND ResourceType IN (...) AND ResourceId IN (...)
            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources",
                columns: new[] { "FacilityId", "ResourceType", "ResourceId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources");

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "ReferenceResources",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ResourceId",
                table: "ReferenceResources",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "ResourceType",
                table: "ReferenceResources",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);
        }
    }
}
