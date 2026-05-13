using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class IX_DataAcquisitionLogs_FacilityId_IsDeleted : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_FacilityId_IsDeleted",
                table: "DataAcquisitionLog",
                column: "FacilityId",
                filter: "[IsDeleted] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_FacilityId_IsDeleted",
                table: "DataAcquisitionLog");
        }
    }
}
