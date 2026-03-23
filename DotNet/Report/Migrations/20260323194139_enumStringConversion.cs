using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class enumStringConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add new string columns
            migrationBuilder.AddColumn<string>(
                name: "Status_New",
                table: "ReportSchedule",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "New");

            migrationBuilder.AddColumn<string>(
                name: "Frequency_New",
                table: "ReportSchedule",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "Discharge");

            migrationBuilder.AddColumn<string>(
                name: "AdHocType_New",
                table: "ReportSchedule",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionStatus_New",
                table: "ReportEntry",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportingStatus_New",
                table: "ReportEntry",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "PatientIdentified");

            migrationBuilder.AddColumn<string>(
                name: "Status_New",
                table: "EntryMeasureReport",
                type: "varchar(255)",
                nullable: false,
                defaultValue: "EntryCreated");

            // 2. Convert existing integer values to string enum names
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status_New] = CASE [Status]
                    WHEN 0 THEN 'New'
                    WHEN 100 THEN 'Scheduled'
                    WHEN 200 THEN 'EndOfPeriod'
                    WHEN 500 THEN 'Submitted'
                    ELSE 'New' END,

                    [Frequency_New] = CASE [Frequency]
                    WHEN 0 THEN 'Discharge'
                    WHEN 1 THEN 'Daily'
                    WHEN 2 THEN 'Weekly'
                    WHEN 3 THEN 'Monthly'
                    WHEN 4 THEN 'Adhoc'
                    ELSE 'Discharge' END,

                    [AdHocType_New] = CASE [AdHocType]
                    WHEN 0 THEN 'Manual'
                    WHEN 1 THEN 'Census'
                    ELSE NULL END;

                UPDATE [ReportEntry]
                SET [ReportingStatus_New] = CASE [ReportingStatus]
                    WHEN 0 THEN 'PatientIdentified'
                    WHEN 1 THEN 'NotReportable'
                    WHEN 2 THEN 'PendingValidation'
                    WHEN 3 THEN 'PassedValidation'
                    WHEN 4 THEN 'FailedValidation'
                    ELSE 'PatientIdentified' END,

                    [SubmissionStatus_New] = CASE [SubmissionStatus]
                    WHEN 0 THEN 'PendingValidation'
                    WHEN 1 THEN 'Submitting'
                    WHEN 2 THEN 'Submitted'
                    WHEN 3 THEN 'FailedSubmission'
                    WHEN 4 THEN 'NotEligable'
                    ELSE NULL END;

                UPDATE [EntryMeasureReport]
                SET [Status_New] = CASE [Status]
                    WHEN 0 THEN 'EntryCreated'
                    WHEN 1 THEN 'NotReportable'
                    WHEN 2 THEN 'ReadyForValidation'
                    ELSE 'EntryCreated' END;
            ");

            // 3. Drop old int columns
            migrationBuilder.DropColumn(name: "Status", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "Frequency", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "AdHocType", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "SubmissionStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "ReportingStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "Status", table: "EntryMeasureReport");

            // 4. Rename new columns to original names
            migrationBuilder.RenameColumn(
                name: "Status_New",
                table: "ReportSchedule",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "Frequency_New",
                table: "ReportSchedule",
                newName: "Frequency");

            migrationBuilder.RenameColumn(
                name: "AdHocType_New",
                table: "ReportSchedule",
                newName: "AdHocType");

            migrationBuilder.RenameColumn(
                name: "SubmissionStatus_New",
                table: "ReportEntry",
                newName: "SubmissionStatus");

            migrationBuilder.RenameColumn(
                name: "ReportingStatus_New",
                table: "ReportEntry",
                newName: "ReportingStatus");

            migrationBuilder.RenameColumn(
                name: "Status_New",
                table: "EntryMeasureReport",
                newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down() reverts to integer columns (simple version – data will be lost on rollback)
            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "ReportSchedule",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<int>(
                name: "Frequency",
                table: "ReportSchedule",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<int>(
                name: "AdHocType",
                table: "ReportSchedule",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SubmissionStatus",
                table: "ReportEntry",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ReportingStatus",
                table: "ReportEntry",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "EntryMeasureReport",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)");
        }
    }
}