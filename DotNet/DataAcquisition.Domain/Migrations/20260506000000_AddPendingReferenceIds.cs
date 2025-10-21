using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingReferenceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[PendingReferenceIds]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [PendingReferenceIds];
                END
            ");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceResourceType",
                table: "DataAcquisitionLog",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PendingReferenceIds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReferenceIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingReferenceIds_DataAcquisitionLog",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingReferenceIds_DataAcquisitionLogId",
                table: "PendingReferenceIds",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "UX_PendingReferenceIds_Log_ResourceId",
                table: "PendingReferenceIds",
                columns: new[] { "DataAcquisitionLogId", "ResourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_DataAcquisitionLogs_ReferenceLogKey",
                table: "DataAcquisitionLog",
                columns: new[] { "FacilityId", "CorrelationId", "QueryPhase", "ReferenceResourceType" },
                unique: true,
                filter: "[CorrelationId] IS NOT NULL AND [QueryPhase] IS NOT NULL AND [ReferenceResourceType] IS NOT NULL");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[PendingReferenceIds]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [PendingReferenceIds];
                END
            ");

            migrationBuilder.DropIndex(
                name: "UX_DataAcquisitionLogs_ReferenceLogKey",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "ReferenceResourceType",
                table: "DataAcquisitionLog");
        }
    }
}
