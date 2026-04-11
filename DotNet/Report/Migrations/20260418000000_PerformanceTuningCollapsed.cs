using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <summary>
    /// Single collapsed migration for the Report performance-tuning branch.
    /// 
    ///  1. RCSI — Enable Read Committed Snapshot Isolation.
    ///  2. IX_ReportResources_Facility_Schedule — covers ReportResource lookups
    ///     by FacilityId + ReportScheduleId + CreateDate.
    ///  3. IX_ReportEntries_Schedule_Patient — covers ReportEntry lookups
    ///     by ReportScheduleId + PatientId.
    /// </summary>
    public partial class PerformanceTuningCollapsed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. RCSI
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);

            // 2. Performance indexes
            migrationBuilder.CreateIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource",
                columns: new[] { "FacilityId", "ReportScheduleId", "CreateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportEntries_Schedule_Patient",
                table: "ReportEntry",
                columns: new[] { "ReportScheduleId", "PatientId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportResources_Facility_Schedule",
                table: "ReportResource");

            migrationBuilder.DropIndex(
                name: "IX_ReportEntries_Schedule_Patient",
                table: "ReportEntry");

            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);
        }
    }
}
