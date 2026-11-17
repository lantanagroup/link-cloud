using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Census.Migrations
{
    /// <inheritdoc />
    public partial class AddQuartzMisfireOrigFireTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarded because the column may already have been added to a deployed database by hand.
            migrationBuilder.Sql(
                "IF COL_LENGTH(N'quartz.QRTZ_TRIGGERS', N'MISFIRE_ORIG_FIRE_TIME') IS NULL " +
                "ALTER TABLE [quartz].[QRTZ_TRIGGERS] ADD [MISFIRE_ORIG_FIRE_TIME] bigint NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH(N'quartz.QRTZ_TRIGGERS', N'MISFIRE_ORIG_FIRE_TIME') IS NOT NULL " +
                "ALTER TABLE [quartz].[QRTZ_TRIGGERS] DROP COLUMN [MISFIRE_ORIG_FIRE_TIME];");
        }
    }
}
