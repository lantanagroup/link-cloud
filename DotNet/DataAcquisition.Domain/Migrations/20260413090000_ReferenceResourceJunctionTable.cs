using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;

#nullable disable

namespace DataAcquisition.Domain.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(DataAcquisitionDbContext))]
    [Migration("20260413090000_ReferenceResourceJunctionTable")]
    public partial class ReferenceResourceJunctionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create the junction table
            migrationBuilder.CreateTable(
                name: "DataAcquisitionLogReferenceResource",
                columns: table => new
                {
                    DataAcquisitionLogId = table.Column<long>(type: "bigint", nullable: false),
                    ReferenceResourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataAcquisitionLogReferenceResource", x => new { x.DataAcquisitionLogId, x.ReferenceResourceId });
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogReferenceResource_DataAcquisitionLog_DataAcquisitionLogId",
                        column: x => x.DataAcquisitionLogId,
                        principalTable: "DataAcquisitionLog",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DataAcquisitionLogReferenceResource_ReferenceResources_ReferenceResourceId",
                        column: x => x.ReferenceResourceId,
                        principalTable: "ReferenceResources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataAcquisitionLogReferenceResource_ReferenceResourceId",
                table: "DataAcquisitionLogReferenceResource",
                column: "ReferenceResourceId");

            // Migrate existing relationships: populate junction table from current FK column.
            // Only rows that have a non-null DataAcquisitionLogId are migrated.
            migrationBuilder.Sql(@"
                INSERT INTO [DataAcquisitionLogReferenceResource] ([DataAcquisitionLogId], [ReferenceResourceId])
                SELECT DISTINCT [DataAcquisitionLogId], [Id]
                FROM [ReferenceResources]
                WHERE [DataAcquisitionLogId] IS NOT NULL;
            ");

            // Drop the old FK constraint(s), index, and column.
            // Constraint names may differ between environments, so detect by table+column.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ReferenceResources', 'DataAcquisitionLogId') IS NOT NULL
                BEGIN
                    DECLARE @fkName sysname;
                    DECLARE @sql nvarchar(500);

                    SELECT TOP 1 @fkName = fk.name
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                    WHERE fk.parent_object_id = OBJECT_ID('ReferenceResources')
                      AND c.name = 'DataAcquisitionLogId';

                    WHILE @fkName IS NOT NULL
                    BEGIN
                        SET @sql = N'ALTER TABLE [ReferenceResources] DROP CONSTRAINT [' + @fkName + N']';
                        EXEC sp_executesql @sql;

                        SET @fkName = NULL;
                        SELECT TOP 1 @fkName = fk.name
                        FROM sys.foreign_keys fk
                        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                        INNER JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
                        WHERE fk.parent_object_id = OBJECT_ID('ReferenceResources')
                          AND c.name = 'DataAcquisitionLogId';
                    END

                    IF EXISTS (
                        SELECT 1
                        FROM sys.indexes
                        WHERE object_id = OBJECT_ID('ReferenceResources')
                          AND name = 'IX_ReferenceResources_DataAcquisitionLogId')
                    BEGIN
                        DROP INDEX [IX_ReferenceResources_DataAcquisitionLogId] ON [ReferenceResources];
                    END

                    ALTER TABLE [ReferenceResources] DROP COLUMN [DataAcquisitionLogId];
                END
            ");

            // Ensure columns are bounded so they can be indexed.
            // The earlier migration 20260412 should have done this, but if
            // columns are still nvarchar(max) we fix them here defensively.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ReferenceResources', 'FacilityId') = -1
                    ALTER TABLE [ReferenceResources] ALTER COLUMN [FacilityId] nvarchar(256) NOT NULL;
                IF COL_LENGTH('ReferenceResources', 'ResourceId') = -1
                    ALTER TABLE [ReferenceResources] ALTER COLUMN [ResourceId] nvarchar(256) NOT NULL;
                IF COL_LENGTH('ReferenceResources', 'ResourceType') = -1
                    ALTER TABLE [ReferenceResources] ALTER COLUMN [ResourceType] nvarchar(128) NOT NULL;
            ");

            // Make the composite index on (FacilityId, ResourceType, ResourceId) unique
            // so only one canonical row exists per facility/type/id combination.
            // First drop the old non-unique index, then recreate as unique.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID('ReferenceResources')
                      AND name = 'IX_ReferenceResources_Facility_Type_ResourceId')
                BEGIN
                    DROP INDEX [IX_ReferenceResources_Facility_Type_ResourceId] ON [ReferenceResources];
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources",
                columns: new[] { "FacilityId", "ResourceType", "ResourceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add the DataAcquisitionLogId column
            migrationBuilder.AddColumn<long>(
                name: "DataAcquisitionLogId",
                table: "ReferenceResources",
                type: "bigint",
                nullable: true);

            // Restore data from junction table (take first log per resource)
            migrationBuilder.Sql(@"
                UPDATE rr
                SET rr.[DataAcquisitionLogId] = j.[DataAcquisitionLogId]
                FROM [ReferenceResources] rr
                INNER JOIN (
                    SELECT [ReferenceResourceId], MIN([DataAcquisitionLogId]) AS [DataAcquisitionLogId]
                    FROM [DataAcquisitionLogReferenceResource]
                    GROUP BY [ReferenceResourceId]
                ) j ON rr.[Id] = j.[ReferenceResourceId];
            ");

            // Recreate old index and FK
            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReferenceResources_DataAcquisitionLog_DataAcquisitionLogId",
                table: "ReferenceResources",
                column: "DataAcquisitionLogId",
                principalTable: "DataAcquisitionLog",
                principalColumn: "Id");

            // Revert unique index to non-unique
            migrationBuilder.DropIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceResources_Facility_Type_ResourceId",
                table: "ReferenceResources",
                columns: new[] { "FacilityId", "ResourceType", "ResourceId" });

            // Drop junction table
            migrationBuilder.DropTable(
                name: "DataAcquisitionLogReferenceResource");
        }
    }
}
