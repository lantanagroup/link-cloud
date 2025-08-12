using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class dataacquisitionlog_implemenation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_referenceResources",
                table: "referenceResources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_fhirQuery",
                table: "fhirQuery");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "fhirQuery");

            migrationBuilder.DropColumn(
                name: "PatientId",
                table: "fhirQuery");

            migrationBuilder.DropColumn(
                name: "RequestBody",
                table: "fhirQuery");

            migrationBuilder.DropColumn(
                name: "SearchParams",
                table: "fhirQuery");

            migrationBuilder.RenameTable(
                name: "referenceResources",
                newName: "ReferenceResources");

            migrationBuilder.RenameTable(
                name: "fhirQuery",
                newName: "FhirQuery");

            migrationBuilder.RenameColumn(
                name: "ResourceType",
                table: "FhirQuery",
                newName: "ResourceTypes");

            migrationBuilder.AddColumn<Guid>(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryPhase",
                table: "ReferenceResources",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "DataAcquisitionLogId",
                table: "FhirQuery",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "Paged",
                table: "FhirQuery",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueryParameters",
                table: "FhirQuery",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "QueryType",
                table: "FhirQuery",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReferenceResources",
                table: "ReferenceResources",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FhirQuery",
                table: "FhirQuery",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "DataAcquisitionLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FhirVersion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueryType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QueryPhase = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecutionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryAttempts = table.Column<int>(type: "int", nullable: true),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionTimeMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ResourceAcquiredIds = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduledReport = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceReferenceType",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QueryPhase = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResourceType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FhirQueryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceReferenceType", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceReferenceType_FhirQuery_FhirQueryId",
                        column: x => x.FhirQueryId,
                        principalTable: "FhirQuery",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReferenceType_FhirQueryId",
                table: "ResourceReferenceType",
                column: "FhirQueryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventRetries");

            migrationBuilder.DropTable(
                name: "fhirListConfiguration");

            migrationBuilder.DropTable(
                name: "fhirQueryConfiguration");

            migrationBuilder.DropTable(
                name: "queryPlan");

            migrationBuilder.DropTable(
                name: "ReferenceResources");

            migrationBuilder.DropTable(
                name: "ResourceReferenceType");

            migrationBuilder.DropTable(
                name: "FhirQuery");

            migrationBuilder.DropTable(
                name: "DataAcquisitionLog");
        }
    }
}
