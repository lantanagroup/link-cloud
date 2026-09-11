using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260827180000_IX_DataAcquisitionLogs_FacilityId_CorrelationId")]
    public partial class IX_DataAcquisitionLogs_FacilityId_CorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_FacilityId_CorrelationId",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "CorrelationId" })
                .Annotation("SqlServer:Include", new[] { "QueryPhase", "Status", "ReportTrackingId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_FacilityId_CorrelationId",
                table: "DataAcquisitionLog");
        }
    }
}
