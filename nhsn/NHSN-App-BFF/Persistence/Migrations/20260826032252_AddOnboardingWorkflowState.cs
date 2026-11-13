using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LantanaGroup.Link.Nhsn.App.Bff.Persistence.Migrations
{
    // Adds the onboarding workflow columns to Facilities and the OnboardingDrafts table, and
    // replaces the stored IsOnboarded boolean with OnboardingStatus.
    //
    // The scaffolded version of this migration had two defects, corrected by hand: EF defaulted
    // OnboardingStatus to the empty string, which is not a valid enum name, and it dropped
    // IsOnboarded without carrying its data across, which would have silently reverted every
    // already-onboarded facility to NotStarted. Both directions now translate the value instead of
    // discarding it.
    //
    // The downgrade is lossy by nature and this is verified, not assumed: a facility that was
    // Complete round-trips intact, but InProgress, Committing and CommitFailed all collapse to 0
    // and come back as NotStarted. One bit can't carry five states — losing which non-complete
    // state a facility was in costs a user their step position, whereas losing Complete would
    // offer an already-onboarded facility the whole flow again.
    public partial class AddOnboardingWorkflowState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OnboardingStatus",
                table: "Facilities",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotStarted");

            // Carry the old boolean across before the column goes. Order matters: this must run
            // while IsOnboarded still exists.
            migrationBuilder.Sql(
                "UPDATE [Facilities] SET [OnboardingStatus] = 'Complete' WHERE [IsOnboarded] = 1;");

            migrationBuilder.DropColumn(
                name: "IsOnboarded",
                table: "Facilities");

            // CompletedOn is deliberately left null for rows migrated to Complete: the old schema
            // never recorded when it happened, and inventing a timestamp would be worse than an
            // honest absence.
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedOn",
                table: "Facilities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentStepId",
                table: "Facilities",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "Facilities",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Facilities",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "Facilities",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Facilities",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedOn",
                table: "Facilities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "Facilities",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OnboardingDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FacilityId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnlockedStepsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OnboardingDrafts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDrafts_FacilityId",
                table: "OnboardingDrafts",
                column: "FacilityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingDrafts");

            migrationBuilder.AddColumn<bool>(
                name: "IsOnboarded",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Translate back rather than defaulting everything to false, which would have told
            // every completed facility to start onboarding again.
            migrationBuilder.Sql(
                "UPDATE [Facilities] SET [IsOnboarded] = 1 WHERE [OnboardingStatus] = 'Complete';");

            migrationBuilder.DropColumn(name: "OnboardingStatus", table: "Facilities");
            migrationBuilder.DropColumn(name: "CompletedOn", table: "Facilities");
            migrationBuilder.DropColumn(name: "CurrentStepId", table: "Facilities");
            migrationBuilder.DropColumn(name: "Vendor", table: "Facilities");
            migrationBuilder.DropColumn(name: "RowVersion", table: "Facilities");
            migrationBuilder.DropColumn(name: "CreatedOn", table: "Facilities");
            migrationBuilder.DropColumn(name: "CreatedBy", table: "Facilities");
            migrationBuilder.DropColumn(name: "LastModifiedOn", table: "Facilities");
            migrationBuilder.DropColumn(name: "LastModifiedBy", table: "Facilities");
        }
    }
}
