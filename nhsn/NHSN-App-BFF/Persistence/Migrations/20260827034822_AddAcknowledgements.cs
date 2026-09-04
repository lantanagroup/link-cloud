using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcknowledgements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Acknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ContextId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    StatementKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StatementVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AcceptedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedByExternalUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acknowledgements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Acknowledgements_FacilityId_Kind_AcceptedOn",
                table: "Acknowledgements",
                columns: new[] { "FacilityId", "Kind", "AcceptedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Acknowledgements");
        }
    }
}
