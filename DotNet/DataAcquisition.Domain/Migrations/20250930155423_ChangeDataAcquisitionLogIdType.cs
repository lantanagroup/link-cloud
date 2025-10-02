using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    public partial class ChangeDataAcquisitionLogIdType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.AddColumn<long>(
                name: "NewId",
                table: "DataAcquisitionLog",
                type: "bigint",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<long>(
                name: "NewDataAcquisitionLogId",
                table: "FhirQuery",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "NewDataAcquisitionLogId",
                table: "ReferenceResources",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE fq
                SET fq.NewDataAcquisitionLogId = dal.NewId
                FROM FhirQuery fq
                INNER JOIN DataAcquisitionLog dal ON fq.DataAcquisitionLogId = dal.Id
            ");

            migrationBuilder.Sql(@"
                UPDATE rr
                SET rr.NewDataAcquisitionLogId = dal.NewId
                FROM ReferenceResources rr
                INNER JOIN DataAcquisitionLog dal ON rr.DataAcquisitionLogId = dal.Id
            ");

            migrationBuilder.Sql(@"
                DELETE FROM FhirQuery
                WHERE NewDataAcquisitionLogId IS NULL
            ");

                        migrationBuilder.Sql(@"
                DELETE FROM ReferenceResources
                WHERE NewDataAcquisitionLogId IS NULL
            ");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.RenameColumn(
                name: "NewDataAcquisitionLogId",
                table: "FhirQuery",
                newName: "DataAcquisitionLogId");

            migrationBuilder.RenameColumn(
                name: "NewDataAcquisitionLogId",
                table: "ReferenceResources",
                newName: "DataAcquisitionLogId");

            migrationBuilder.AlterColumn<long>(
                name: "DataAcquisitionLogId",
                table: "FhirQuery",
                type: "bigint",
                nullable: false,
                oldNullable: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DataAcquisitionLog");

            migrationBuilder.RenameColumn(
                name: "NewId",
                table: "DataAcquisitionLog",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.AddColumn<Guid>(
                name: "NewId",
                table: "DataAcquisitionLog",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            migrationBuilder.AddColumn<Guid>(
                name: "NewDataAcquisitionLogId",
                table: "FhirQuery",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NewDataAcquisitionLogId",
                table: "ReferenceResources",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE fq
                SET fq.NewDataAcquisitionLogId = dal.NewId
                FROM FhirQuery fq
                INNER JOIN DataAcquisitionLog dal ON fq.DataAcquisitionLogId = dal.Id
            ");

            migrationBuilder.Sql(@"
                UPDATE rr
                SET rr.NewDataAcquisitionLogId = dal.NewId
                FROM ReferenceResources rr
                INNER JOIN DataAcquisitionLog dal ON rr.DataAcquisitionLogId = dal.Id
            ");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources");

            migrationBuilder.RenameColumn(
                name: "NewDataAcquisitionLogId",
                table: "FhirQuery",
                newName: "DataAcquisitionLogId");

            migrationBuilder.RenameColumn(
                name: "NewDataAcquisitionLogId",
                table: "ReferenceResources",
                newName: "DataAcquisitionLogId");

            migrationBuilder.AlterColumn<Guid>(
                name: "DataAcquisitionLogId",
                table: "FhirQuery",
                type: "uniqueidentifier",
                nullable: false,
                oldNullable: true);

            migrationBuilder.DropPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DataAcquisitionLog");

            migrationBuilder.RenameColumn(
                name: "NewId",
                table: "DataAcquisitionLog",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_FhirQuery_DataAcquisitionLogId",
                table: "FhirQuery",
                column: "DataAcquisitionLogId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");
        }
    }
}