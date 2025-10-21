using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Status_ExecutionDate_Index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Status_ExecutionDate",
                table: "DataAcquisitionLog",
                columns: new[] { "Status", "ExecutionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogs_Status_ModifyDate",
                table: "DataAcquisitionLog",
                columns: new[] { "Status", "ModifyDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Status_ExecutionDate",
                table: "DataAcquisitionLog");

            migrationBuilder.DropIndex(
                name: "IX_DataAcquisitionLogs_Status_ModifyDate",
                table: "DataAcquisitionLog");
        }
    }
}
