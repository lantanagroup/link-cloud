using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueryDispatch.Migrations
{
    /// <inheritdoc />
    public partial class AddQuartzMisfireOrigFireTimeAndDropEventRetries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRetries");

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

            migrationBuilder.CreateTable(
                name: "EventRetries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ScheduledTrigger = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventRetries", x => x.Id);
                });
        }
    }
}
