using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class LNK3598_REMOVE_QUERYPLANIDS_FHIRQUERYCONFIG : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QueryPlanIds",
                table: "fhirQueryConfiguration");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QueryPlanIds",
                table: "fhirQueryConfiguration",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
