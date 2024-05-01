using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class ChangeKafkaTableName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl");

            migrationBuilder.RenameTable(
                name: "kafkaRetryTbl",
                newName: "RetryEvent");

            migrationBuilder.AddPrimaryKey(
                name: "PK_RetryEvent",
                table: "RetryEvent",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_RetryEvent",
                table: "RetryEvent");

            migrationBuilder.RenameTable(
                name: "RetryEvent",
                newName: "kafkaRetryTbl");

            migrationBuilder.AddPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl",
                column: "Id");
        }
    }
}
