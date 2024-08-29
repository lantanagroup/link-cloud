using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Audit.Migrations
{
    /// <inheritdoc />
    public partial class _20240822_RenameRetryEntityTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl");

            migrationBuilder.RenameTable(
                name: "kafkaRetryTbl",
                newName: "EventRetries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EventRetries",
                table: "EventRetries",
                column: "Id")
                .Annotation("SqlServer:Clustered", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_EventRetries",
                table: "EventRetries");

            migrationBuilder.RenameTable(
                name: "EventRetries",
                newName: "kafkaRetryTbl");

            migrationBuilder.AddPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl",
                column: "Id")
                .Annotation("SqlServer:Clustered", false);
        }
    }
}
