using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexResourceTypeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ResourceType",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldUnicode: false,
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceType_Name",
                table: "ResourceType",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceType_Name",
                table: "ResourceType");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "ResourceType",
                type: "varchar(255)",
                unicode: false,
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldUnicode: false,
                oldMaxLength: 255);
        }
    }
}
