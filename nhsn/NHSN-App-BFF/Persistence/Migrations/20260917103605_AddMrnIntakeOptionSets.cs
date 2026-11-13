using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMrnIntakeOptionSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MrnIntakeOptionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionGroup = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    LabelKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MrnIntakeOptionSets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MrnIntakeOptionSets_OptionGroup_Value",
                table: "MrnIntakeOptionSets",
                columns: new[] { "OptionGroup", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MrnIntakeOptionSets");
        }
    }
}
