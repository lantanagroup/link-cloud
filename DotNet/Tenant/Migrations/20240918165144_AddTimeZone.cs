using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // verify the column does not exist before adding it
            migrationBuilder.AddColumn<string>(
                name: "TimeZone",
                table: "Facilities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "America/New_York");
                
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZone",
                table: "Facilities");
        }
    }
}
