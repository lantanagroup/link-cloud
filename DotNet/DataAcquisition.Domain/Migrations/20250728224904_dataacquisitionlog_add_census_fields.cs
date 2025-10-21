using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class dataacquisitionlog_add_census_fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CensusListId",
                table: "FhirQuery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CensusPatientStatus",
                table: "FhirQuery",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CensusTimeFrame",
                table: "FhirQuery",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CensusListId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "CensusPatientStatus",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "CensusTimeFrame",
                table: "FhirQuery");
        }
    }
}
