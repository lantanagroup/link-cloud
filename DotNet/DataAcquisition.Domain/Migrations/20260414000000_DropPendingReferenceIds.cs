using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260414000000_DropPendingReferenceIds")]
    public partial class DropPendingReferenceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingReferenceIds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingReferenceIds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FhirQueryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingReferenceIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PendingReferenceIds_FhirQuery_FhirQueryId",
                        column: x => x.FhirQueryId,
                        principalTable: "FhirQuery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PendingReferenceIds_FhirQueryId",
                table: "PendingReferenceIds",
                column: "FhirQueryId");

            migrationBuilder.CreateIndex(
                name: "UX_PendingReferenceIds_FhirQueryId_ResourceType_ResourceId",
                table: "PendingReferenceIds",
                columns: new[] { "FhirQueryId", "ResourceType", "ResourceId" },
                unique: true);
        }
    }
}
