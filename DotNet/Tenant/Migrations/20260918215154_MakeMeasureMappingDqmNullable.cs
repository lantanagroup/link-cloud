using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class MakeMeasureMappingDqmNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DQM",
                table: "MeasureMappings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rows the sync recorded for a measure Link has no dQM for. The column cannot go back to
            // NOT NULL while they hold NULL, and they cannot simply be deleted because reporting
            // plans reference them (the foreign key restricts it). The empty string is what the
            // column held for an unset dQM before this migration, so restoring it is a faithful
            // reversal rather than an invention.
            migrationBuilder.Sql("UPDATE [MeasureMappings] SET [DQM] = '' WHERE [DQM] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "DQM",
                table: "MeasureMappings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
