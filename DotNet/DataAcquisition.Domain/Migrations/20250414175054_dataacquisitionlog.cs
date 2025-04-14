using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class dataacquisitionlog : Migration
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
                    Priority = table.Column<int>(type: "int", nullable: false),
                    PatientId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FhirVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    QueryType = table.Column<int>(type: "int", nullable: false),
                    QueryPhase = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExecutionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryAttempts = table.Column<int>(type: "int", nullable: false),
                    CompletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletionTimeMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    ResourceAcquiredIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    QueryPhase = table.Column<int>(type: "int", nullable: false),
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
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceReferenceType_FhirQueryId",
                table: "ResourceReferenceType",
                column: "FhirQueryId");

            migrationBuilder.AddForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog_DataAcquisitionLogId",
                table: "FhirQuery",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.DropTable(
                name: "DataAcquisitionLog");

            migrationBuilder.DropTable(
                name: "ResourceReferenceType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReferenceResources",
                table: "ReferenceResources");

            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FhirQuery",
                table: "FhirQuery");

            migrationBuilder.DropIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.DropColumn(
                name: "QueryPhase",
                table: "ReferenceResources");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "Paged",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "QueryParameters",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "QueryType",
                table: "FhirQuery");

            migrationBuilder.RenameTable(
                name: "ReferenceResources",
                newName: "referenceResources");

            migrationBuilder.RenameTable(
                name: "FhirQuery",
                newName: "fhirQuery");

            migrationBuilder.RenameColumn(
                name: "ResourceTypes",
                table: "fhirQuery",
                newName: "ResourceType");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "fhirQuery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientId",
                table: "fhirQuery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestBody",
                table: "fhirQuery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SearchParams",
                table: "fhirQuery",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_referenceResources",
                table: "referenceResources",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_fhirQuery",
                table: "fhirQuery",
                column: "Id");
        }
    }
}
