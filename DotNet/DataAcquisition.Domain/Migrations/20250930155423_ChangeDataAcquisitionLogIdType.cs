using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    public partial class ChangeDataAcquisitionLogIdType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop foreign keys to allow changes
            migrationBuilder.DropForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources");

            // Step 2: Add new bigint identity column to DataAcquisitionLog (SQL Server will auto-populate existing rows)
            migrationBuilder.AddColumn<long>(
                name: "NewId",
                table: "DataAcquisitionLog",
                type: "bigint",
                nullable: false)
                .Annotation("SqlServer:Identity", "1, 1");

            // Step 3: Add new bigint columns to referencing tables (nullable initially)
            migrationBuilder.AddColumn<long>(
                name: "NewDataAcquisitionLogId",
                table: "FhirQuery",
                type: "bigint",
                nullable: true);  // Temporarily nullable to allow update

            migrationBuilder.AddColumn<long>(
                name: "NewDataAcquisitionLogId",
                table: "ReferenceResources",
                type: "bigint",
                nullable: true);

            // Step 4: Update new FK columns with mapped values from new PK (using raw SQL for join/update)
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

            // Step 5: Drop old FK columns
            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources");

            // Step 6: Rename new FK columns to original names and adjust nullability
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

            // Step 7: Drop old PK constraint and old Id column in DataAcquisitionLog
            migrationBuilder.DropPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DataAcquisitionLog");

            // Step 8: Rename new Id column and add PK
            migrationBuilder.RenameColumn(
                name: "NewId",
                table: "DataAcquisitionLog",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog",
                column: "Id");

            // Step 9: Re-add foreign keys
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
            // Note: Downgrading back to Guid would require regenerating Guids or similar mapping.
            // This Down method assumes data preservation but will assign new Guids to Id (potential reference break if not handled).
            // If Down is critical, adjust accordingly.

            // Step 1: Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_FhirQuery_DataAcquisitionLog_DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources");

            // Step 2: Add new Guid column to DataAcquisitionLog
            migrationBuilder.AddColumn<Guid>(
                name: "NewId",
                table: "DataAcquisitionLog",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");

            // Step 3: Add new Guid columns to referencing tables
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

            // Step 4: Update new FK columns (map back using current long Id to new Guid)
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

            // Step 5: Drop old FK columns
            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "FhirQuery");

            migrationBuilder.DropColumn(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources");

            // Step 6: Rename new FK columns and adjust nullability
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

            // Step 7: Drop old PK and old Id
            migrationBuilder.DropPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "DataAcquisitionLog");

            // Step 8: Rename new Id and add PK (no identity, as Guid isn't identity-based)
            migrationBuilder.RenameColumn(
                name: "NewId",
                table: "DataAcquisitionLog",
                newName: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DataAcquisitionLog",
                table: "DataAcquisitionLog",
                column: "Id");

            // Step 9: Re-add foreign keys
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