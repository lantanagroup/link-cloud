using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class StoreResourceTypeCountsPerLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataAcquisitionLogResourceTypeCounts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLogResourceTypeCounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogResourceTypeCounts_DataAcquisitionLog_DataAcquisitionLogId",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_DataAcquisitionLogResourceTypeCounts_Log_ResourceType",
                table: "DataAcquisitionLogResourceTypeCounts",
                columns: new[] { "DataAcquisitionLogId", "ResourceType" },
                unique: true)
                .Annotation("SqlServer:Include", new[] { "Count" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataAcquisitionLogResourceTypeCounts");
        }
    }
}
