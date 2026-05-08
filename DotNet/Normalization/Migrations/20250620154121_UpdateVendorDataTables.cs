using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class UpdateVendorDataTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorPresetOperationResourceType");

            migrationBuilder.DropTable(
                name: "VendorOperationPreset");

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

            migrationBuilder.CreateTable(
                name: "VendorVersionOperationPreset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VendorVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationResourceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOperationPreset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorOperationPreset_OperationResourceTypes",
                        column: x => x.OperationResourceTypeId,
                        principalTable: "OperationResourceTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VendorOperationPreset_VendorVersion",
                        column: x => x.VendorVersionId,
                        principalTable: "VendorVersion",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersion_VendorId",
                table: "VendorVersion",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersionOperationPreset_OperationResourceTypeId",
                table: "VendorVersionOperationPreset",
                column: "OperationResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorVersionOperationPreset_VendorVersionId",
                table: "VendorVersionOperationPreset",
                column: "VendorVersionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorVersionOperationPreset");

            migrationBuilder.DropTable(
                name: "VendorVersion");

            migrationBuilder.DropTable(
                name: "Vendor");

            migrationBuilder.CreateTable(
                name: "VendorOperationPreset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    Description = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Vendor = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Versions = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorOperationPreset", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VendorPresetOperationResourceType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    OperationResourceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VendorOperationPresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPresetOperationResourceType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPresetOperationResourceTypes_OperationResourceTypes",
                        column: x => x.OperationResourceTypeId,
                        principalTable: "OperationResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorPresetOperationResourceTypes_VendorOperationPreset",
                        column: x => x.VendorOperationPresetId,
                        principalTable: "VendorOperationPreset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorPresetOperationResourceType_OperationResourceTypeId",
                table: "VendorPresetOperationResourceType",
                column: "OperationResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorPresetOperationResourceType_VendorOperationPresetId",
                table: "VendorPresetOperationResourceType",
                column: "VendorOperationPresetId");
        }
    }
}
