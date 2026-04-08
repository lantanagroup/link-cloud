using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <summary>
    /// Enables Read Committed Snapshot Isolation (RCSI) on the Report database.
    /// 
    /// Without RCSI, readers take shared (S) locks that block writers with exclusive (X) locks.
    /// During the multi-patient pipeline, ReportResource is under heavy concurrent
    /// insert pressure (MeasureReportGenerated events) while polling queries scan the
    /// same table. This reader/writer contention causes 24+ second SQL blocks.
    /// 
    /// With RCSI, readers use row versioning in tempdb instead of shared locks,
    /// completely eliminating this contention while still guaranteeing consistent reads.
    /// </summary>
    public partial class EnableReadCommittedSnapshotIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT OFF WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);
        }
    }
}
