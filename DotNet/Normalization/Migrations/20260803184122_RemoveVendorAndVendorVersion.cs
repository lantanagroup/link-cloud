using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class RemoveVendorAndVendorVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VendorOperationPreset_VendorVersion",
                table: "VendorVersionOperationPreset");

            migrationBuilder.DropTable(
                name: "VendorVersion");

            migrationBuilder.DropTable(
                name: "Vendor");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vendor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorVersion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorVersion_Vendor",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id");
                });

            migrationBuilder.Sql("""
                INSERT INTO [Vendor] ([Id], [Name])
                SELECT DISTINCT [VendorVersionId], CONVERT(varchar(36), [VendorVersionId])
                FROM [VendorVersionOperationPreset];

                INSERT INTO [VendorVersion] ([Id], [VendorId], [Version])
                SELECT DISTINCT [VendorVersionId], [VendorVersionId], 'restored'
                FROM [VendorVersionOperationPreset];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersion_VendorId",
                table: "VendorVersion",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_VendorOperationPreset_VendorVersion",
                table: "VendorVersionOperationPreset",
                column: "VendorVersionId",
                principalTable: "VendorVersion",
                principalColumn: "Id");
        }
    }
}
