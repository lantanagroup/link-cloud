using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class RecordUnmappedMeasureMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old index allowed one measure to carry several dQMs; the new one does not. Stop with
            // a message that says what to do rather than letting CREATE INDEX fail on a duplicate key,
            // which reports the collision without saying why it is now a collision.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MeasureMappings] GROUP BY [Measure] HAVING COUNT(*) > 1)
    THROW 50000, 'MeasureMappings holds more than one row for the same measure. A measure now maps to exactly one dQM, so the duplicates must be resolved before this migration can be applied.', 1;
");

            migrationBuilder.DropIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings");

            migrationBuilder.AlterColumn<string>(
                name: "DQM",
                table: "MeasureMappings",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255);

            migrationBuilder.CreateIndex(
                name: "IX_MeasureMappings_Measure",
                table: "MeasureMappings",
                column: "Measure",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MeasureMappings_Measure",
                table: "MeasureMappings");

            // Rows the sync recorded for a measure Link has no dQM for. The column cannot go back to
            // NOT NULL while they hold NULL, and they cannot simply be deleted because reporting plans
            // reference them. The empty string is what the column held for an unset dQM before this
            // migration, so restoring it is a faithful reversal rather than an invention.
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

            migrationBuilder.CreateIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings",
                columns: new[] { "Measure", "DQM" },
                unique: true);
        }
    }
}
