using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class Add_Operation_Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Operation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    FacilityId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    OperationJson = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    OperationType = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "varchar(max)", unicode: false, nullable: true),
                    IsDisabled = table.Column<bool>(type: "bit", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    Name = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationResourceTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationResourceTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationResourceTypes_Operation",
                        column: x => x.OperationId,
                        principalTable: "Operation",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OperationResourceTypes_ResourceType",
                        column: x => x.ResourceTypeId,
                        principalTable: "ResourceType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OperationSequence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperationResourceTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "(getutcdate())"),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationSequence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationSequence_OperationResourceTypes",
                        column: x => x.OperationResourceTypeId,
                        principalTable: "OperationResourceTypes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationResourceTypes_OperationId",
                table: "OperationResourceTypes",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationResourceTypes_ResourceTypeId",
                table: "OperationResourceTypes",
                column: "ResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationSequence_OperationResourceTypeId",
                table: "OperationSequence",
                column: "OperationResourceTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationSequence");

            migrationBuilder.DropTable(
                name: "OperationResourceTypes");

            migrationBuilder.DropTable(
                name: "Operation");

            migrationBuilder.DropTable(
                name: "ResourceType");
        }
    }
}
