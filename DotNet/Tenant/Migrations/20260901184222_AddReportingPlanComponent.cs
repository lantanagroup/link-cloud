using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingPlanComponent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfilled to MSC rather than to the empty string EF defaults to. Every row that
            // exists predates components and came from the medicine operation; an empty component
            // would fail validation the first time one of those rows was updated.
            migrationBuilder.AddColumn<string>(
                name: "Component",
                table: "FacilityReportingPlans",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "MSC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Component",
                table: "FacilityReportingPlans");
        }
    }
}
