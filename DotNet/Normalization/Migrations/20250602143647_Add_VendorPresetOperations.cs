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
                name: "VendorPresetOperationSequences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    VendorOperationPresetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationSequenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorPresetOperationSequences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorPresetOperationSequenceMap_OperationSequence",
                        column: x => x.OperationSequenceId,
                        principalTable: "OperationSequence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_VendorPresetOperationSequenceMap_VendorOperationPreset",
                        column: x => x.VendorOperationPresetId,
                        principalTable: "VendorOperationPreset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "CPK_VendorOperationMap",
                table: "VendorPresetOperationSequences",
                columns: new[] { "VendorOperationPresetId", "OperationSequenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VendorPresetOperationSequences_OperationSequenceId",
                table: "VendorPresetOperationSequences",
                column: "OperationSequenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorPresetOperationSequences");

            migrationBuilder.DropTable(
                name: "VendorOperationPreset");
        }
    }
}
