using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class StoreResourceTypeCountsPerLog : Migration
    {
        /// <summary>
        /// Fills the counts for every log that has resource ids but no counts yet, with the same rule the service
        /// uses when it saves them. Safe to run again: logs that already have counts are skipped.
        /// </summary>
        public const string BackfillSql = """
            INSERT INTO [DataAcquisitionLogResourceTypeCounts] ([DataAcquisitionLogId], [ResourceType], [Count])
            SELECT r.[DataAcquisitionLogId], x.[ResourceType], COUNT(*)
            FROM [DataAcquisitionLogResourceIds] AS r
            CROSS APPLY (SELECT CASE
                                    WHEN CHARINDEX(N'/', r.[ResourceId]) > 1
                                        THEN SUBSTRING(r.[ResourceId], 1, CHARINDEX(N'/', r.[ResourceId]) - 1)
                                    ELSE r.[ResourceId]
                                END AS [ResourceType]) AS x
            WHERE r.[ResourceId] <> N'' AND
                  NOT EXISTS (SELECT 1
                              FROM [DataAcquisitionLogResourceTypeCounts] AS c
                              WHERE c.[DataAcquisitionLogId] = r.[DataAcquisitionLogId])
            GROUP BY r.[DataAcquisitionLogId], x.[ResourceType];
            """;

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

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataAcquisitionLogResourceTypeCounts");
        }
    }
}
