using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasureMappingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DQM",
                table: "MeasureMappings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "MeasureMappings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Measure",
                table: "MeasureMappings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings",
                columns: new[] { "Measure", "DQM" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings");

            migrationBuilder.DropColumn(
                name: "DQM",
                table: "MeasureMappings");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "MeasureMappings");

            migrationBuilder.DropColumn(
                name: "Measure",
                table: "MeasureMappings");
        }
    }
}
