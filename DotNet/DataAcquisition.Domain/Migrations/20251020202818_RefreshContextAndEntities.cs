using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    public partial class RefreshContextAndEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ResourceType",
                table: "ResourceReferenceType",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
          
            migrationBuilder.DropIndex(
                    name: "IX_ResourceReferenceType_FhirQueryId",
                    table: "ResourceReferenceType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ResourceReferenceType",
                table: "ResourceReferenceType");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ResourceReferenceType",
                type: "uniqueidentifier",
                defaultValueSql: "(newid())",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ResourceReferenceType",
                table: "ResourceReferenceType",
                column: "Id");

            migrationBuilder.CreateIndex(
                    name: "IX_ResourceReferenceType_FhirQueryId",
                    table: "ResourceReferenceType",
                    column: "FhirQueryId");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReferenceResources",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "FhirQuery",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "queryPlan",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "fhirQueryConfiguration",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "fhirListConfiguration",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "(newid())",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResourceAcquiredIds",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryType",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "QueryPhase",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                defaultValue: "[]",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog",
                table: "FhirQuery",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog",
                table: "FhirQuery");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog",
                table: "ReferenceResources");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceType",
                table: "ResourceReferenceType",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Id",
                table: "ResourceReferenceType",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceResource",
                table: "ReferenceResources",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "ReferenceResources",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "queryPlan",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "fhirQueryConfiguration",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "fhirListConfiguration",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "(newid())");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "ResourceAcquiredIds",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "QueryType",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "QueryPhase",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "DataAcquisitionLog",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

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
    }
}
