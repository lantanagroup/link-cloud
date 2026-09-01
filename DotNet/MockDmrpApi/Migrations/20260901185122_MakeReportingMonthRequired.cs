using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.MockDmrpApi.Migrations
{
    /// <inheritdoc />
    public partial class MakeReportingMonthRequired : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Entries stored without a month were recorded when the patient-safety plan was
            // believed to be annual. Both components are reported monthly, so there is no
            // month to infer for them, and letting the alter default them to 0 would leave
            // rows that no query matches and that validation rejects on the next write. They
            // are seeded fixtures, so removing them is the honest outcome.
            migrationBuilder.Sql("DELETE FROM MockDmrpEntries WHERE ReportingMonth IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "ReportingMonth",
                table: "MockDmrpEntries",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "ReportingMonth",
                table: "MockDmrpEntries",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
