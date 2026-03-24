using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class enumStringConversion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop indexes that reference the columns being converted
            migrationBuilder.DropIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry");

            // --- ReportSchedule: convert int columns to string in-place ---

            // Status: int NOT NULL -> nvarchar(450) NOT NULL
            // First widen to nvarchar (SQL Server implicitly casts 0->'0', 100->'100', etc.)
            migrationBuilder.AlterColumn<string>(
                name: "Status", table: "ReportSchedule",
                type: "nvarchar(450)", nullable: false, defaultValue: "New",
                oldClrType: typeof(int), oldType: "int");
            // Then map the old numeric strings to enum names
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status] = CASE [Status]
                    WHEN '0' THEN 'New' WHEN '100' THEN 'Scheduled'
                    WHEN '200' THEN 'EndOfPeriod' WHEN '500' THEN 'Submitted'
                    ELSE 'New' END;");

            // Frequency: int NOT NULL -> nvarchar(max) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Frequency", table: "ReportSchedule",
                type: "nvarchar(max)", nullable: false, defaultValue: "Discharge",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Frequency] = CASE [Frequency]
                    WHEN '0' THEN 'Discharge' WHEN '1' THEN 'Daily'
                    WHEN '2' THEN 'Weekly' WHEN '3' THEN 'Monthly'
                    WHEN '4' THEN 'Adhoc' ELSE 'Discharge' END;");

            // AdHocType: int NULL -> nvarchar(max) NULL
            migrationBuilder.AlterColumn<string>(
                name: "AdHocType", table: "ReportSchedule",
                type: "nvarchar(max)", nullable: true,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [AdHocType] = CASE [AdHocType]
                    WHEN '0' THEN 'Manual' WHEN '1' THEN 'Census' ELSE NULL END;");

            // --- ReportEntry: convert int columns to string in-place ---

            // ReportingStatus: int NOT NULL -> nvarchar(450) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "ReportingStatus", table: "ReportEntry",
                type: "nvarchar(450)", nullable: false, defaultValue: "PatientIdentified",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [ReportingStatus] = CASE [ReportingStatus]
                    WHEN '0' THEN 'PatientIdentified' WHEN '1' THEN 'NotReportable'
                    WHEN '2' THEN 'PendingValidation' WHEN '3' THEN 'PassedValidation'
                    WHEN '4' THEN 'FailedValidation' ELSE 'PatientIdentified' END;");

            // SubmissionStatus: int NULL -> nvarchar(450) NULL
            migrationBuilder.AlterColumn<string>(
                name: "SubmissionStatus", table: "ReportEntry",
                type: "nvarchar(450)", nullable: true,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [SubmissionStatus] = CASE [SubmissionStatus]
                    WHEN '0' THEN 'PendingValidation' WHEN '1' THEN 'Submitting'
                    WHEN '2' THEN 'Submitted' WHEN '3' THEN 'FailedSubmission'
                    WHEN '4' THEN 'NotEligable' ELSE NULL END;");

            // --- EntryMeasureReport: convert int column to string in-place ---

            // Status: int NOT NULL -> nvarchar(max) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Status", table: "EntryMeasureReport",
                type: "nvarchar(max)", nullable: false, defaultValue: "EntryCreated",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                UPDATE [EntryMeasureReport]
                SET [Status] = CASE [Status]
                    WHEN '0' THEN 'EntryCreated' WHEN '1' THEN 'NotReportable'
                    WHEN '2' THEN 'ReadyForValidation' ELSE 'EntryCreated' END;");

            // Recreate indexes on the now-string columns
            migrationBuilder.CreateIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry", columns: new[] { "ReportingStatus", "SubmissionStatus" });
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry", columns: new[] { "ReportScheduleId", "ReportingStatus" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes
            migrationBuilder.DropIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry");

            // --- ReportSchedule: convert string columns back to int in-place ---

            // Map enum names back to numeric strings, then alter to int
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status] = CASE [Status]
                    WHEN 'New' THEN '0' WHEN 'Scheduled' THEN '100'
                    WHEN 'EndOfPeriod' THEN '200' WHEN 'Submitted' THEN '500'
                    ELSE '0' END;");
            migrationBuilder.AlterColumn<int>(
                name: "Status", table: "ReportSchedule",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(450)");

            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Frequency] = CASE [Frequency]
                    WHEN 'Discharge' THEN '0' WHEN 'Daily' THEN '1'
                    WHEN 'Weekly' THEN '2' WHEN 'Monthly' THEN '3'
                    WHEN 'Adhoc' THEN '4' ELSE '0' END;");
            migrationBuilder.AlterColumn<int>(
                name: "Frequency", table: "ReportSchedule",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(max)");

            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [AdHocType] = CASE [AdHocType]
                    WHEN 'Manual' THEN '0' WHEN 'Census' THEN '1' ELSE NULL END;");
            migrationBuilder.AlterColumn<int>(
                name: "AdHocType", table: "ReportSchedule",
                type: "int", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(max)", oldNullable: true);

            // --- ReportEntry: convert string columns back to int in-place ---

            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [ReportingStatus] = CASE [ReportingStatus]
                    WHEN 'PatientIdentified' THEN '0' WHEN 'NotReportable' THEN '1'
                    WHEN 'PendingValidation' THEN '2' WHEN 'PassedValidation' THEN '3'
                    WHEN 'FailedValidation' THEN '4' ELSE '0' END;");
            migrationBuilder.AlterColumn<int>(
                name: "ReportingStatus", table: "ReportEntry",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(450)");

            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [SubmissionStatus] = CASE [SubmissionStatus]
                    WHEN 'PendingValidation' THEN '0' WHEN 'Submitting' THEN '1'
                    WHEN 'Submitted' THEN '2' WHEN 'FailedSubmission' THEN '3'
                    WHEN 'NotEligable' THEN '4' ELSE NULL END;");
            migrationBuilder.AlterColumn<int>(
                name: "SubmissionStatus", table: "ReportEntry",
                type: "int", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(450)", oldNullable: true);

            // --- EntryMeasureReport: convert string column back to int in-place ---

            migrationBuilder.Sql(@"
                UPDATE [EntryMeasureReport]
                SET [Status] = CASE [Status]
                    WHEN 'EntryCreated' THEN '0' WHEN 'NotReportable' THEN '1'
                    WHEN 'ReadyForValidation' THEN '2' ELSE '0' END;");
            migrationBuilder.AlterColumn<int>(
                name: "Status", table: "EntryMeasureReport",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(max)");

            // Recreate indexes
            migrationBuilder.CreateIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry", columns: new[] { "ReportingStatus", "SubmissionStatus" });
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry", columns: new[] { "ReportScheduleId", "ReportingStatus" });
        }
    }
}