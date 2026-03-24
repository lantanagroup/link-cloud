using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Report.Migrations
{
    /// <inheritdoc />
    public partial class enumStringConversion : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop indexes that reference the columns we're dropping
            migrationBuilder.DropIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry");

            // Add new string columns
            migrationBuilder.AddColumn<string>(name: "Status_New", table: "ReportSchedule", type: "varchar(255)", nullable: false, defaultValue: "New");
            migrationBuilder.AddColumn<string>(name: "Frequency_New", table: "ReportSchedule", type: "varchar(255)", nullable: false, defaultValue: "Discharge");
            migrationBuilder.AddColumn<string>(name: "AdHocType_New", table: "ReportSchedule", type: "varchar(255)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "SubmissionStatus_New", table: "ReportEntry", type: "varchar(255)", nullable: true);
            migrationBuilder.AddColumn<string>(name: "ReportingStatus_New", table: "ReportEntry", type: "varchar(255)", nullable: false, defaultValue: "PatientIdentified");
            migrationBuilder.AddColumn<string>(name: "Status_New", table: "EntryMeasureReport", type: "varchar(255)", nullable: false, defaultValue: "EntryCreated");

            // Convert int → string
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status_New] = CASE [Status]
                    WHEN 0 THEN 'New' WHEN 100 THEN 'Scheduled' 
                    WHEN 200 THEN 'EndOfPeriod' WHEN 500 THEN 'Submitted' 
                    ELSE 'New' END,
                    [Frequency_New] = CASE [Frequency]
                    WHEN 0 THEN 'Discharge' WHEN 1 THEN 'Daily' 
                    WHEN 2 THEN 'Weekly' WHEN 3 THEN 'Monthly' 
                    WHEN 4 THEN 'Adhoc' ELSE 'Discharge' END,
                    [AdHocType_New] = CASE [AdHocType]
                    WHEN 0 THEN 'Manual' WHEN 1 THEN 'Census' ELSE NULL END;

                UPDATE [ReportEntry]
                SET [ReportingStatus_New] = CASE [ReportingStatus]
                    WHEN 0 THEN 'PatientIdentified' WHEN 1 THEN 'NotReportable' 
                    WHEN 2 THEN 'PendingValidation' WHEN 3 THEN 'PassedValidation' 
                    WHEN 4 THEN 'FailedValidation' ELSE 'PatientIdentified' END,
                    [SubmissionStatus_New] = CASE [SubmissionStatus]
                    WHEN 0 THEN 'PendingValidation' WHEN 1 THEN 'Submitting' 
                    WHEN 2 THEN 'Submitted' WHEN 3 THEN 'FailedSubmission' 
                    WHEN 4 THEN 'NotEligable' ELSE NULL END;

                UPDATE [EntryMeasureReport]
                SET [Status_New] = CASE [Status]
                    WHEN 0 THEN 'EntryCreated' WHEN 1 THEN 'NotReportable' 
                    WHEN 2 THEN 'ReadyForValidation' ELSE 'EntryCreated' END;
            ");

            // Drop old int columns
            migrationBuilder.DropColumn(name: "Status", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "Frequency", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "AdHocType", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "SubmissionStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "ReportingStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "Status", table: "EntryMeasureReport");

            // Rename new columns back to original names
            migrationBuilder.RenameColumn(name: "Status_New", table: "ReportSchedule", newName: "Status");
            migrationBuilder.RenameColumn(name: "Frequency_New", table: "ReportSchedule", newName: "Frequency");
            migrationBuilder.RenameColumn(name: "AdHocType_New", table: "ReportSchedule", newName: "AdHocType");
            migrationBuilder.RenameColumn(name: "SubmissionStatus_New", table: "ReportEntry", newName: "SubmissionStatus");
            migrationBuilder.RenameColumn(name: "ReportingStatus_New", table: "ReportEntry", newName: "ReportingStatus");
            migrationBuilder.RenameColumn(name: "Status_New", table: "EntryMeasureReport", newName: "Status");

            // Recreate indexes on the new string columns
            migrationBuilder.CreateIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry", columns: new[] { "ReportingStatus", "SubmissionStatus" });
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry", columns: new[] { "ReportScheduleId", "ReportingStatus" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes again
            migrationBuilder.DropIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry");
            migrationBuilder.DropIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry");

            // Add back the original integer columns
            migrationBuilder.AddColumn<int>(name: "Status_Old", table: "ReportSchedule", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "Frequency_Old", table: "ReportSchedule", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "AdHocType_Old", table: "ReportSchedule", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "SubmissionStatus_Old", table: "ReportEntry", type: "int", nullable: true);
            migrationBuilder.AddColumn<int>(name: "ReportingStatus_Old", table: "ReportEntry", type: "int", nullable: false, defaultValue: 0);
            migrationBuilder.AddColumn<int>(name: "Status_Old", table: "EntryMeasureReport", type: "int", nullable: false, defaultValue: 0);

            // Convert string → original int values (reversible)
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status_Old] = CASE [Status]
                    WHEN 'New' THEN 0 WHEN 'Scheduled' THEN 100 
                    WHEN 'EndOfPeriod' THEN 200 WHEN 'Submitted' THEN 500 
                    ELSE 0 END,
                    [Frequency_Old] = CASE [Frequency]
                    WHEN 'Discharge' THEN 0 WHEN 'Daily' THEN 1 
                    WHEN 'Weekly' THEN 2 WHEN 'Monthly' THEN 3 
                    WHEN 'Adhoc' THEN 4 ELSE 0 END,
                    [AdHocType_Old] = CASE [AdHocType]
                    WHEN 'Manual' THEN 0 WHEN 'Census' THEN 1 ELSE NULL END;

                UPDATE [ReportEntry]
                SET [ReportingStatus_Old] = CASE [ReportingStatus]
                    WHEN 'PatientIdentified' THEN 0 WHEN 'NotReportable' THEN 1 
                    WHEN 'PendingValidation' THEN 2 WHEN 'PassedValidation' THEN 3 
                    WHEN 'FailedValidation' THEN 4 ELSE 0 END,
                    [SubmissionStatus_Old] = CASE [SubmissionStatus]
                    WHEN 'PendingValidation' THEN 0 WHEN 'Submitting' THEN 1 
                    WHEN 'Submitted' THEN 2 WHEN 'FailedSubmission' THEN 3 
                    WHEN 'NotEligable' THEN 4 ELSE NULL END;

                UPDATE [EntryMeasureReport]
                SET [Status_Old] = CASE [Status]
                    WHEN 'EntryCreated' THEN 0 WHEN 'NotReportable' THEN 1 
                    WHEN 'ReadyForValidation' THEN 2 ELSE 0 END;
            ");

            // Drop the string columns
            migrationBuilder.DropColumn(name: "Status", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "Frequency", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "AdHocType", table: "ReportSchedule");
            migrationBuilder.DropColumn(name: "SubmissionStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "ReportingStatus", table: "ReportEntry");
            migrationBuilder.DropColumn(name: "Status", table: "EntryMeasureReport");

            // Rename old int columns back to original names
            migrationBuilder.RenameColumn(name: "Status_Old", table: "ReportSchedule", newName: "Status");
            migrationBuilder.RenameColumn(name: "Frequency_Old", table: "ReportSchedule", newName: "Frequency");
            migrationBuilder.RenameColumn(name: "AdHocType_Old", table: "ReportSchedule", newName: "AdHocType");
            migrationBuilder.RenameColumn(name: "SubmissionStatus_Old", table: "ReportEntry", newName: "SubmissionStatus");
            migrationBuilder.RenameColumn(name: "ReportingStatus_Old", table: "ReportEntry", newName: "ReportingStatus");
            migrationBuilder.RenameColumn(name: "Status_Old", table: "EntryMeasureReport", newName: "Status");

            // Recreate indexes
            migrationBuilder.CreateIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry", columns: new[] { "ReportingStatus", "SubmissionStatus" });
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry", columns: new[] { "ReportScheduleId", "ReportingStatus" });
        }
    }
}