using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <summary>
    /// Enables Read Committed Snapshot Isolation (RCSI) on the database.
    /// 
    /// With RCSI, all READ COMMITTED queries (the EF Core default) automatically use
    /// row versioning in tempdb instead of acquiring shared (S) locks. This eliminates
    /// the deadlock pattern where concurrent readers hold S-locks that conflict with
    /// exclusive (X) locks from writer transactions (ExecuteUpdateAsync, SaveChangesAsync).
    /// 
    /// Unlike READ UNCOMMITTED / NOLOCK, RCSI guarantees consistent, committed data —
    /// readers see the last committed version of each row, not in-flight changes.
    /// 
    /// This is a one-time, database-level setting that persists across restarts.
    /// The ALTER requires exclusive access so it briefly blocks new connections.
    /// </summary>
    public partial class EnableReadCommittedSnapshotIsolation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DB_NAME() resolves to the current database — no hard-coded name needed.
            // suppressTransaction is required because ALTER DATABASE cannot execute
            // inside the implicit transaction that EF Core wraps around each migration.
            migrationBuilder.Sql(@"
                DECLARE @dbname NVARCHAR(256) = DB_NAME();
                DECLARE @sql NVARCHAR(MAX) = 
                    N'ALTER DATABASE ' + QUOTENAME(@dbname) + N' SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE';
                EXEC sp_executesql @sql;
            ", suppressTransaction: true);
        }

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
