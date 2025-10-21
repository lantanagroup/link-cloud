using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class addVendor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "Facilities");

            migrationBuilder.AddColumn<long>(
                name: "MISFIRE_ORIG_FIRE_TIME",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendorVersionId",
                table: "Facilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vendor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorVersion_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_VendorVersionId",
                table: "Facilities",
                column: "VendorVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersion_VendorId",
                table: "VendorVersion",
                column: "VendorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_VendorVersion_VendorVersionId",
                table: "Facilities",
                column: "VendorVersionId",
                principalTable: "VendorVersion",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_VendorVersion_VendorVersionId",
                table: "Facilities");

            migrationBuilder.DropTable(
                name: "VendorVersion");

            migrationBuilder.DropTable(
                name: "Vendor");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_VendorVersionId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MISFIRE_ORIG_FIRE_TIME",
                schema: "quartz",
                table: "QRTZ_TRIGGERS");

            migrationBuilder.DropColumn(
                name: "VendorVersionId",
                table: "Facilities");

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "Facilities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
