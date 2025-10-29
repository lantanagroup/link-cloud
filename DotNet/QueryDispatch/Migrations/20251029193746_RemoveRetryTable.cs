using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QueryDispatch.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRetries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
