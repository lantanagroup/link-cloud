using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Census.Migrations
{
    /// <inheritdoc />
    public partial class LNK_3255_POI_Add_Indexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourcePatientId",
                table: "PatientEvents",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "PatientEvents",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "PatientEvents",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "PatientEncounters",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "PatientEncounters",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "Enabled",
                table: "CensusConfig",
                type: "bit",
                nullable: true,
                defaultValue: true);
            
            migrationBuilder.Sql("UPDATE CensusConfig SET Enabled = 1");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_CorrelationId",
                table: "PatientEvents",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_CorrelationId_CreateDate",
                table: "PatientEvents",
                columns: new[] { "CorrelationId", "CreateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_CreateDate",
                table: "PatientEvents",
                column: "CreateDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_FacilityId",
                table: "PatientEvents",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEvents_SourcePatientId",
                table: "PatientEvents",
                column: "SourcePatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_AdmitDate",
                table: "PatientEncounters",
                column: "AdmitDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_CorrelationId",
                table: "PatientEncounters",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_DischargeDate",
                table: "PatientEncounters",
                column: "DischargeDate");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_FacilityId",
                table: "PatientEncounters",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_FacilityId_AdmitDate",
                table: "PatientEncounters",
                columns: new[] { "FacilityId", "AdmitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_FacilityId_DischargeDate",
                table: "PatientEncounters",
                columns: new[] { "FacilityId", "DischargeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientEncounters_Id",
                table: "PatientEncounters",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_CorrelationId",
                table: "PatientEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_CorrelationId_CreateDate",
                table: "PatientEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_CreateDate",
                table: "PatientEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_FacilityId",
                table: "PatientEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientEvents_SourcePatientId",
                table: "PatientEvents");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_AdmitDate",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_CorrelationId",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_DischargeDate",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_FacilityId",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_FacilityId_AdmitDate",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_FacilityId_DischargeDate",
                table: "PatientEncounters");

            migrationBuilder.DropIndex(
                name: "IX_PatientEncounters_Id",
                table: "PatientEncounters");

            migrationBuilder.DropColumn(
                name: "Enabled",
                table: "CensusConfig");

            migrationBuilder.AlterColumn<string>(
                name: "SourcePatientId",
                table: "PatientEvents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "PatientEvents",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "PatientEvents",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FacilityId",
                table: "PatientEncounters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "PatientEncounters",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
