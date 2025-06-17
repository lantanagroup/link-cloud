using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class Fix_Retry_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "queriedFhirResource");

            migrationBuilder.DropPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl");

            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "queryPlan");

            migrationBuilder.RenameTable(
                name: "kafkaRetryTbl",
                newName: "EventRetries");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "queryPlan",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_EventRetries",
                table: "EventRetries",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "fhirQuery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResourceType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SearchParams = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fhirQuery", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fhirQuery");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EventRetries",
                table: "EventRetries");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "queryPlan");

            migrationBuilder.RenameTable(
                name: "EventRetries",
                newName: "kafkaRetryTbl");

            migrationBuilder.AddColumn<string>(
                name: "ReportType",
                table: "queryPlan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_kafkaRetryTbl",
                table: "kafkaRetryTbl",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "queriedFhirResource",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FacilityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueryType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResourceId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResourceType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queriedFhirResource", x => x.Id);
                });
        }
    }
}
