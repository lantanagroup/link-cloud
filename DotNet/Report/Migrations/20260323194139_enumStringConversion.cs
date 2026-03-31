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

            // Status: int NOT NULL -> nvarchar(50) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Status", table: "ReportSchedule",
                type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "New",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [Status] NOT IN ('0','100','200','500'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.Status contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status] = CASE [Status]
                    WHEN '0' THEN 'New' WHEN '100' THEN 'Scheduled'
                    WHEN '200' THEN 'EndOfPeriod' WHEN '500' THEN 'Submitted'
                END;");

            // Frequency: int NOT NULL -> nvarchar(50) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Frequency", table: "ReportSchedule",
                type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Discharge",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [Frequency] NOT IN ('0','1','2','3','4'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.Frequency contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Frequency] = CASE [Frequency]
                    WHEN '0' THEN 'Discharge' WHEN '1' THEN 'Daily'
                    WHEN '2' THEN 'Weekly' WHEN '3' THEN 'Monthly'
                    WHEN '4' THEN 'Adhoc'
                END;");

            // AdHocType: int NULL -> nvarchar(50) NULL
            migrationBuilder.AlterColumn<string>(
                name: "AdHocType", table: "ReportSchedule",
                type: "nvarchar(50)", maxLength: 50, nullable: true,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [AdHocType] IS NOT NULL AND [AdHocType] NOT IN ('0','1'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.AdHocType contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [AdHocType] = CASE [AdHocType]
                    WHEN '0' THEN 'Manual' WHEN '1' THEN 'Census'
                END;");

            // --- ReportEntry: convert int columns to string in-place ---

            // ReportingStatus: int NOT NULL -> nvarchar(50) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "ReportingStatus", table: "ReportEntry",
                type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "PatientIdentified",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportEntry] WHERE [ReportingStatus] NOT IN ('0','1','2','3','4'))
                BEGIN
                    ;THROW 50001, 'ReportEntry.ReportingStatus contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [ReportingStatus] = CASE [ReportingStatus]
                    WHEN '0' THEN 'PatientIdentified' WHEN '1' THEN 'NotReportable'
                    WHEN '2' THEN 'PendingValidation' WHEN '3' THEN 'PassedValidation'
                    WHEN '4' THEN 'FailedValidation'
                END;");

            // SubmissionStatus: int NULL -> nvarchar(50) NULL
            migrationBuilder.AlterColumn<string>(
                name: "SubmissionStatus", table: "ReportEntry",
                type: "nvarchar(50)", maxLength: 50, nullable: true,
                oldClrType: typeof(int), oldType: "int", oldNullable: true);
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportEntry] WHERE [SubmissionStatus] IS NOT NULL AND [SubmissionStatus] NOT IN ('0','1','2','3','4'))
                BEGIN
                    ;THROW 50001, 'ReportEntry.SubmissionStatus contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [SubmissionStatus] = CASE [SubmissionStatus]
                    WHEN '0' THEN 'PendingValidation' WHEN '1' THEN 'Submitting'
                    WHEN '2' THEN 'Submitted' WHEN '3' THEN 'FailedSubmission'
                    WHEN '4' THEN 'NotEligable'
                END;");

            // --- EntryMeasureReport: convert int column to string in-place ---

            // Status: int NOT NULL -> nvarchar(50) NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "Status", table: "EntryMeasureReport",
                type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "EntryCreated",
                oldClrType: typeof(int), oldType: "int");
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [EntryMeasureReport] WHERE [Status] NOT IN ('0','1','2'))
                BEGIN
                    ;THROW 50001, 'EntryMeasureReport.Status contains unmapped values. Migration aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [EntryMeasureReport]
                SET [Status] = CASE [Status]
                    WHEN '0' THEN 'EntryCreated' WHEN '1' THEN 'NotReportable'
                    WHEN '2' THEN 'ReadyForValidation'
                END;");

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

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [Status] NOT IN ('New','Scheduled','EndOfPeriod','Submitted'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.Status contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Status] = CASE [Status]
                    WHEN 'New' THEN '0' WHEN 'Scheduled' THEN '100'
                    WHEN 'EndOfPeriod' THEN '200' WHEN 'Submitted' THEN '500'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "Status", table: "ReportSchedule",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50);

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [Frequency] NOT IN ('Discharge','Daily','Weekly','Monthly','Adhoc'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.Frequency contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [Frequency] = CASE [Frequency]
                    WHEN 'Discharge' THEN '0' WHEN 'Daily' THEN '1'
                    WHEN 'Weekly' THEN '2' WHEN 'Monthly' THEN '3'
                    WHEN 'Adhoc' THEN '4'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "Frequency", table: "ReportSchedule",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50);

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportSchedule] WHERE [AdHocType] IS NOT NULL AND [AdHocType] NOT IN ('Manual','Census'))
                BEGIN
                    ;THROW 50001, 'ReportSchedule.AdHocType contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportSchedule]
                SET [AdHocType] = CASE [AdHocType]
                    WHEN 'Manual' THEN '0' WHEN 'Census' THEN '1'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "AdHocType", table: "ReportSchedule",
                type: "int", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50, oldNullable: true);

            // --- ReportEntry: convert string columns back to int in-place ---

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportEntry] WHERE [ReportingStatus] NOT IN ('PatientIdentified','NotReportable','PendingValidation','PassedValidation','FailedValidation'))
                BEGIN
                    ;THROW 50001, 'ReportEntry.ReportingStatus contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [ReportingStatus] = CASE [ReportingStatus]
                    WHEN 'PatientIdentified' THEN '0' WHEN 'NotReportable' THEN '1'
                    WHEN 'PendingValidation' THEN '2' WHEN 'PassedValidation' THEN '3'
                    WHEN 'FailedValidation' THEN '4'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "ReportingStatus", table: "ReportEntry",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50);

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [ReportEntry] WHERE [SubmissionStatus] IS NOT NULL AND [SubmissionStatus] NOT IN ('PendingValidation','Submitting','Submitted','FailedSubmission','NotEligable'))
                BEGIN
                    ;THROW 50001, 'ReportEntry.SubmissionStatus contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [ReportEntry]
                SET [SubmissionStatus] = CASE [SubmissionStatus]
                    WHEN 'PendingValidation' THEN '0' WHEN 'Submitting' THEN '1'
                    WHEN 'Submitted' THEN '2' WHEN 'FailedSubmission' THEN '3'
                    WHEN 'NotEligable' THEN '4'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "SubmissionStatus", table: "ReportEntry",
                type: "int", nullable: true,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50, oldNullable: true);

            // --- EntryMeasureReport: convert string column back to int in-place ---

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM [EntryMeasureReport] WHERE [Status] NOT IN ('EntryCreated','NotReportable','ReadyForValidation'))
                BEGIN
                    ;THROW 50001, 'EntryMeasureReport.Status contains unmapped values. Rollback aborted.', 1;
                END");
            migrationBuilder.Sql(@"
                UPDATE [EntryMeasureReport]
                SET [Status] = CASE [Status]
                    WHEN 'EntryCreated' THEN '0' WHEN 'NotReportable' THEN '1'
                    WHEN 'ReadyForValidation' THEN '2'
                END;");
            migrationBuilder.AlterColumn<int>(
                name: "Status", table: "EntryMeasureReport",
                type: "int", nullable: false, defaultValue: 0,
                oldClrType: typeof(string), oldType: "nvarchar(50)", oldMaxLength: 50);

            // Recreate indexes
            migrationBuilder.CreateIndex(name: "IX_ReportSchedules_Status", table: "ReportSchedule", column: "Status");
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Reporting_Submission", table: "ReportEntry", columns: new[] { "ReportingStatus", "SubmissionStatus" });
            migrationBuilder.CreateIndex(name: "IX_ReportEntries_Schedule_Status", table: "ReportEntry", columns: new[] { "ReportScheduleId", "ReportingStatus" });
        }
    }
}