using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizationReportActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NormalizationReportActivities",
                columns: table => new
                {
                    ReportTrackingId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    CorrelationId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    QueryType = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    FacilityId = table.Column<string>(type: "varchar(255)", unicode: false, maxLength: 255, nullable: false),
                    Resources = table.Column<int>(type: "int", nullable: false),
                    Failures = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NormalizationReportActivities", x => new { x.ReportTrackingId, x.CorrelationId, x.QueryType });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NormalizationReportActivities");
        }
    }
}
