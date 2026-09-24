using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class IndexMeasureMappingsByMeasure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Split out from the column change on purpose. Making DQM nullable is always safe, but
            // this index is not: the old one allowed a measure to carry several dQMs, so an
            // environment can hold rows it now forbids. Tenant migrates at startup (AutoMigrateEF,
            // on by default) and nothing catches a failure, so applying both together would mean the
            // service failing to boot on such an environment. Separating them lets the column ship
            // while the duplicates are resolved, and leaves this one to be applied deliberately.
            //
            // The duplicates are not resolved here: merging them would pick one dQM and discard
            // another that somebody chose, silently changing what the facilities on it report. That
            // is a decision for whoever owns the data, not for a migration.
            //
            // TABLOCKX/HOLDLOCK because the check and the index creation are not the same instant:
            // during a rolling deploy the instances still serving traffic could insert a second row
            // for a measure in between. The lock is held to the end of this migration's transaction.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [MeasureMappings] WITH (TABLOCKX, HOLDLOCK) GROUP BY [Measure] HAVING COUNT(*) > 1)
    THROW 50000, 'MeasureMappings holds more than one row for the same measure. A measure now maps to exactly one dQM, so the duplicates must be resolved before this migration can be applied.', 1;
");

            migrationBuilder.DropIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings");

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

            migrationBuilder.CreateIndex(
                name: "IX_MeasureMappings_Measure_DQM",
                table: "MeasureMappings",
                columns: new[] { "Measure", "DQM" },
                unique: true);
        }
    }
}
