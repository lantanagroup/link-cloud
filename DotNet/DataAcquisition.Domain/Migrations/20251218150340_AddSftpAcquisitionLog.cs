using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class AddSftpAcquisitionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SftpAcquisitionLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    FacilityName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PatientCount = table.Column<int>(type: "int", nullable: false),
                    EncounterCount = table.Column<int>(type: "int", nullable: false),
                    ProcessDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SftpAcquisitionLog", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SftpAcquisitionLog_FacilityId",
                table: "SftpAcquisitionLog",
                column: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SftpAcquisitionLog");
        }
    }
}
