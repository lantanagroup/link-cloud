using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Audit.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditPropertyChangeRowIdentity : Migration
    {
        /// <inheritdoc />
        //Entity and RowKey need no DDL: PropertyChanges is one JSON column, so the change is in the
        //model snapshot alone. The Quartz column is the AppAny.Quartz 0.5.2 model catching up with
        //Audit, which had no migration generated since the bump - the same way it reached Tenant,
        //Normalization, Report and Data Acquisition.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "MISFIRE_ORIG_FIRE_TIME",
                schema: "quartz",
                table: "QRTZ_TRIGGERS",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MISFIRE_ORIG_FIRE_TIME",
                schema: "quartz",
                table: "QRTZ_TRIGGERS");
        }
    }
}
