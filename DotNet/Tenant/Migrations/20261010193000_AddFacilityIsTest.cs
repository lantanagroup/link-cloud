using LantanaGroup.Link.Tenant.Repository.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <summary>
    /// Existing rows read as not a test facility. Downgrade drops the column.
    /// </summary>
    [DbContext(typeof(TenantDbContext))]
    [Migration("20261010193000_AddFacilityIsTest")]
    public class AddFacilityIsTest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTest",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTest",
                table: "Facilities");
        }
    }
}
