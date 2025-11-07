using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Census.Migrations
{
    /// <inheritdoc />
    public partial class AddEventDateToPatientEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            DateTime defaultDateTimeValue = default;

            migrationBuilder.AddColumn<DateTime>(
                name: "EventDate",
                table: "PatientEvents",
                type: "datetime2",
                nullable: true);

            // Populate EventDate based on EventType and Payload JSON
            migrationBuilder.Sql(@"
            UPDATE PatientEvents
            SET EventDate = 
                CASE 
                    WHEN EventType = 'FHIRListAdmit' THEN 
                        JSON_VALUE(Payload, '$.admitDate')
                    WHEN EventType = 'FHIRListDischarge' THEN 
                        JSON_VALUE(Payload, '$.dischargeDate')
                    ELSE 
                        CreateDate  -- fallback to CreateDate if no specific date in payload
                END
            WHERE EventType IN ('FHIRListAdmit', 'FHIRListDischarge')
               OR EventType IS NULL");

            // Handle any remaining NULLs (optional: set to CreateDate)
            migrationBuilder.Sql(@"
            UPDATE PatientEvents
            SET EventDate = CreateDate
            WHERE EventDate IS NULL");

            // Alter column to NOT NULL
            migrationBuilder.AlterColumn<DateTime>(
                name: "EventDate",
                table: "PatientEvents",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_EventDate",
                table: "PatientEvents",
                column: "EventDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_EventDate",
                table: "PatientEvents");

            migrationBuilder.DropColumn(
                name: "EventDate",
                table: "PatientEvents");

        }
    }
}
