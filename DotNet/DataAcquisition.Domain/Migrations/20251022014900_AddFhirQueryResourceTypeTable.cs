using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddFhirQueryResourceTypeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResourceTypes",
                table: "FhirQuery");

            migrationBuilder.CreateTable(
                name: "FhirQueryResourceType",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "(newid())"),
                    FhirQueryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FhirQueryResourceType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FhirQueryResourceType_FhirQuery",
                        column: x => x.FhirQueryId,
                        principalTable: "FhirQuery",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FhirQueryResourceType_FhirQueryId",
                table: "FhirQueryResourceType",
                column: "FhirQueryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FhirQueryResourceType");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ResourceReferenceType",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ResourceTypes",
                table: "FhirQuery",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
