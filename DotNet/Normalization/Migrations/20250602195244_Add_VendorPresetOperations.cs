using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class Add_VendorPresetOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationResourceTypes_Operation",
                table: "OperationResourceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationResourceTypes_ResourceType",
                table: "OperationResourceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationSequence_OperationResourceTypes",
                table: "OperationSequence");

            migrationBuilder.CreateTable(
                name: "VendorOperationPreset",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Vendor = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Versions = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    Description = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                    VendorOperationPresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationResourceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
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

            migrationBuilder.AddForeignKey(
                name: "FK_OperationResourceTypes_Operation",
                table: "OperationResourceTypes",
                column: "OperationId",
                principalTable: "Operation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationResourceTypes_ResourceType",
                table: "OperationResourceTypes",
                column: "ResourceTypeId",
                principalTable: "ResourceType",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_OperationSequence_OperationResourceTypes",
                table: "OperationSequence",
                column: "OperationResourceTypeId",
                principalTable: "OperationResourceTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationResourceTypes_Operation",
                table: "OperationResourceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationResourceTypes_ResourceType",
                table: "OperationResourceTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_OperationSequence_OperationResourceTypes",
                table: "OperationSequence");

            migrationBuilder.DropTable(
                name: "VendorPresetOperationResourceType");

            migrationBuilder.DropTable(
                name: "VendorOperationPreset");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationResourceTypes_Operation",
                table: "OperationResourceTypes",
                column: "OperationId",
                principalTable: "Operation",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationResourceTypes_ResourceType",
                table: "OperationResourceTypes",
                column: "ResourceTypeId",
                principalTable: "ResourceType",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationSequence_OperationResourceTypes",
                table: "OperationSequence",
                column: "OperationResourceTypeId",
                principalTable: "OperationResourceTypes",
                principalColumn: "Id");
        }
    }
}
