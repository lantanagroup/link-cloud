using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Operations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutorLeases",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ExpiresUnixMs = table.Column<long>(type: "bigint", nullable: false),
                    Fence = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutorLeases", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "KafkaChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(249)", maxLength: 249, nullable: false),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUnixMs = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUnixMs = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KafkaChangeRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KafkaTopicMigrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Environment = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(249)", maxLength: 249, nullable: false),
                    Fence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedUnixMs = table.Column<long>(type: "bigint", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KafkaTopicMigrations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KafkaChangeRequests_Environment_CreatedUnixMs",
                table: "KafkaChangeRequests",
                columns: new[] { "Environment", "CreatedUnixMs" });

            migrationBuilder.CreateIndex(
                name: "IX_KafkaChangeRequests_Environment_IsOpen",
                table: "KafkaChangeRequests",
                columns: new[] { "Environment", "IsOpen" });

            migrationBuilder.CreateIndex(
                name: "IX_KafkaTopicMigrations_Environment",
                table: "KafkaTopicMigrations",
                column: "Environment");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutorLeases");

            migrationBuilder.DropTable(
                name: "KafkaChangeRequests");

            migrationBuilder.DropTable(
                name: "KafkaTopicMigrations");
        }
    }
}
