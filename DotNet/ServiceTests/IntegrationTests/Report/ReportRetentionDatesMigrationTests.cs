using LantanaGroup.Link.Report.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report;

/// <summary>
/// Proves the LEGLINK-1245 backfill on real SQL Server. Rows that existed before the migration get
/// CompletedDate and SoftDeletedDate by the rule approved on LEGLINK-1243. The test reaches the
/// pre-migration schema through the migration's own Down, so both directions are exercised.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class ReportRetentionDatesMigrationTests
{
    private const string PreviousMigration = "20260827211554_AddReportEntryMappingOutcome";

    private static readonly DateTime CreateDate = new(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ModifyDate = new(2026, 2, 20, 9, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime SubmitDate = new(2026, 2, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset PeriodStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PeriodEnd = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IServiceScopeFactory _scopeFactory;

    public ReportRetentionDatesMigrationTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task Up_RowsFromBeforeTheMigration_GetDatesByTheApprovedRule()
    {
        var facilityId = $"retention-{Guid.NewGuid():N}";
        var submitted = Guid.NewGuid();
        var submittedWithoutSubmitDate = Guid.NewGuid();
        var deletedSubmitted = Guid.NewGuid();
        var bypassed = Guid.NewGuid();
        var bypassedNeverModified = Guid.NewGuid();
        var deletedScheduled = Guid.NewGuid();
        var deletedEndOfPeriodNeverModified = Guid.NewGuid();
        var scheduled = Guid.NewGuid();

        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var migrator = context.Database.GetService<IMigrator>();

            await migrator.MigrateAsync(PreviousMigration);
            try
            {
                await InsertAsync(context, submitted, facilityId, "Submitted", false, SubmitDate, ModifyDate);
                await InsertAsync(context, submittedWithoutSubmitDate, facilityId, "Submitted", false, null, ModifyDate);
                await InsertAsync(context, deletedSubmitted, facilityId, "Submitted", true, SubmitDate, ModifyDate);
                await InsertAsync(context, bypassed, facilityId, "CompletedNotSubmitted", false, null, ModifyDate);
                await InsertAsync(context, bypassedNeverModified, facilityId, "CompletedNotSubmitted", false, null, null);
                await InsertAsync(context, deletedScheduled, facilityId, "Scheduled", true, null, ModifyDate);
                await InsertAsync(context, deletedEndOfPeriodNeverModified, facilityId, "EndOfPeriod", true, null, null);
                await InsertAsync(context, scheduled, facilityId, "Scheduled", false, null, ModifyDate);
            }
            finally
            {
                // Leave the schema at the latest migration for the other tests on this fixture.
                await migrator.MigrateAsync();
            }
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
            var rows = await context.ReportSchedule
                .AsNoTracking()
                .Where(r => r.FacilityId == facilityId)
                .ToDictionaryAsync(r => r.Id);

            Assert.Equal(8, rows.Count);

            Assert.Equal(Utc(SubmitDate), rows[submitted].CompletedDate);
            Assert.Null(rows[submitted].SoftDeletedDate);

            Assert.Equal(Utc(ModifyDate), rows[submittedWithoutSubmitDate].CompletedDate);

            Assert.Equal(Utc(SubmitDate), rows[deletedSubmitted].CompletedDate);
            Assert.Null(rows[deletedSubmitted].SoftDeletedDate);

            Assert.Equal(Utc(ModifyDate), rows[bypassed].CompletedDate);
            Assert.Null(rows[bypassed].SoftDeletedDate);

            Assert.Equal(Utc(CreateDate), rows[bypassedNeverModified].CompletedDate);

            Assert.Null(rows[deletedScheduled].CompletedDate);
            Assert.Equal(Utc(ModifyDate), rows[deletedScheduled].SoftDeletedDate);

            Assert.Null(rows[deletedEndOfPeriodNeverModified].CompletedDate);
            Assert.Equal(Utc(CreateDate), rows[deletedEndOfPeriodNeverModified].SoftDeletedDate);

            Assert.Null(rows[scheduled].CompletedDate);
            Assert.Null(rows[scheduled].SoftDeletedDate);
        }
    }

    private static DateTimeOffset Utc(DateTime value)
    {
        return new DateTimeOffset(value, TimeSpan.Zero);
    }

    /// <summary>
    /// Raw SQL because the entity already has the new columns, which do not exist at the previous migration.
    /// </summary>
    private static Task InsertAsync(ReportDbContext context,
                                    Guid id,
                                    string facilityId,
                                    string status,
                                    bool isDeleted,
                                    DateTime? submitReportDateTime,
                                    DateTime? modifyDate)
    {
        return context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO [ReportSchedule]
                 ([Id], [CreateDate], [ModifyDate], [FacilityId], [ReportStartDate], [ReportEndDate],
                  [SubmitReportDateTime], [EnableSubmission], [EndOfReportPeriodJobHasRun], [Frequency],
                  [Status], [IsDeleted])
             VALUES
                 ({id}, {CreateDate}, {modifyDate}, {facilityId}, {PeriodStart}, {PeriodEnd},
                  {submitReportDateTime}, 1, 1, 'Monthly', {status}, {isDeleted});
             """);
    }
}
