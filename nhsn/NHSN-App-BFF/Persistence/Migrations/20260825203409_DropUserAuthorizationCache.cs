using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    // Removes the cached copies of the groups and facility claims from Users. Both are now derived
    // from the validated token per request.
    //
    // The EF warning about data loss is expected and harmless — both columns were written from the
    // token on every request and nothing reads them any more. Do not reinstate either column: they
    // are a second source of authorization data that can drift from the token.
    public partial class DropUserAuthorizationCache : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GroupsRaw",
                table: "Users");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilityId",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupsRaw",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }
    }
}
