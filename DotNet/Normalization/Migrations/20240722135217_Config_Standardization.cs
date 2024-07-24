using System;
using System.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.ValueGeneration;

#nullable disable

namespace LantanaGroup.Link.Normalization.Migrations
{
    /// <inheritdoc />
    public partial class Config_Standardization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "NormalizationConfig");

            migrationBuilder.RenameColumn(
                name: "ModifiedDate",
                table: "NormalizationConfig",
                newName: "ModifyDate");

            migrationBuilder.DropPrimaryKey("PK_NormalizationConfig", "NormalizationConfig");

            migrationBuilder.DropColumn("Id", "NormalizationConfig");

            migrationBuilder.AddColumn<Guid>("Id", "NormalizationConfig", nullable: false, defaultValueSql: "NEWID()");

            migrationBuilder.AddPrimaryKey("PK_NormalizationConfig", "NormalizationConfig", "Id");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "NormalizationConfig",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "(getutcdate())");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "NormalizationConfig");

            migrationBuilder.RenameColumn(
                name: "ModifyDate",
                table: "NormalizationConfig",
                newName: "ModifiedDate");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "NormalizationConfig",
                type: "int",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedDate",
                table: "NormalizationConfig",
                type: "datetime2",
                nullable: true,
                defaultValueSql: "(getutcdate())");
        }
    }
}
