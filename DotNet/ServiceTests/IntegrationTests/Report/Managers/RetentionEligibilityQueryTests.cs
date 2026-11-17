using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Covers the retention eligibility rule on real SQL Server: which date governs, the strict cutoff, soft-deleted
/// reports, and the order and cap.
/// </summary>
/// <remarks>
/// The fixture's database is shared by the whole collection, and the query reads every facility. Each test
/// therefore works in its own year long before any real data, and asserts only on the reports it seeded. The
/// cap test uses the oldest year of all, so nothing seeded by another test sorts ahead of its reports.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class RetentionEligibilityQueryTests
{
    private const string FacilityId = "retention-eligibility-facility";
    private const int NoCap = 10_000;

    private readonly IServiceScopeFactory _scopeFactory;

    public RetentionEligibilityQueryTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task FindAsync_CompletedBeforeCutoff_SelectsIt()
    {
        var cutoff = Cutoff(1901);
        var report = await SeedAsync(completed: cutoff.AddDays(-1));

        var found = await FindAsync(cutoff, NoCap);

        Assert.Contains(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_CompletedExactlyAtCutoff_DoesNotSelectIt()
    {
        var cutoff = Cutoff(1902);
        var atCutoff = await SeedAsync(completed: cutoff);
        var oneTickBefore = await SeedAsync(completed: cutoff.AddTicks(-1));

        var found = await FindAsync(cutoff, NoCap);

        Assert.DoesNotContain(found, r => r.ReportScheduleId == atCutoff);
        Assert.Contains(found, r => r.ReportScheduleId == oneTickBefore);
    }

    [Fact]
    public async Task FindAsync_CompletedAfterCutoff_DoesNotSelectIt()
    {
        var cutoff = Cutoff(1903);
        var report = await SeedAsync(completed: cutoff.AddDays(1));

        var found = await FindAsync(cutoff, NoCap);

        Assert.DoesNotContain(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_UnfinishedSoftDeletedBeforeCutoff_SelectsIt()
    {
        var cutoff = Cutoff(1904);
        var report = await SeedAsync(softDeleted: cutoff.AddDays(-1), isDeleted: true);

        var found = await FindAsync(cutoff, NoCap);

        Assert.Contains(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_UnfinishedSoftDeletedAfterCutoff_DoesNotSelectIt()
    {
        var cutoff = Cutoff(1905);
        var report = await SeedAsync(softDeleted: cutoff.AddDays(1), isDeleted: true);

        var found = await FindAsync(cutoff, NoCap);

        Assert.DoesNotContain(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_FinishedThenSoftDeleted_AgesFromCompletedDate()
    {
        var cutoff = Cutoff(1906);
        var oldAndDeleted = await SeedAsync(completed: cutoff.AddDays(-1), isDeleted: true);
        var recentAndDeleted = await SeedAsync(completed: cutoff.AddDays(1), isDeleted: true);

        // Never written by Report, but it pins which date wins if both are ever present.
        var recentWithOldDelete = await SeedAsync(completed: cutoff.AddDays(1),
                                                  softDeleted: cutoff.AddDays(-1),
                                                  isDeleted: true);

        var found = await FindAsync(cutoff, NoCap);

        Assert.Contains(found, r => r.ReportScheduleId == oldAndDeleted);
        Assert.DoesNotContain(found, r => r.ReportScheduleId == recentAndDeleted);
        Assert.DoesNotContain(found, r => r.ReportScheduleId == recentWithOldDelete);
    }

    [Fact]
    public async Task FindAsync_NeverFinishedNorSoftDeleted_NeverSelectsIt()
    {
        var cutoff = Cutoff(1907);
        var report = await SeedAsync(created: cutoff.AddYears(-1));

        var found = await FindAsync(cutoff, NoCap);

        Assert.DoesNotContain(found, r => r.ReportScheduleId == report);
    }

    [Theory]
    [InlineData(ReportRetentionStatus.PendingDeletion)]
    [InlineData(ReportRetentionStatus.Scrubbed)]
    public async Task FindAsync_AlreadyInRetention_DoesNotSelectIt(ReportRetentionStatus status)
    {
        var cutoff = Cutoff(1908);
        var report = await SeedAsync(completed: cutoff.AddDays(-1), status: status);

        var found = await FindAsync(cutoff, NoCap);

        Assert.DoesNotContain(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_CutoffWithOffset_ComparesTheInstant()
    {
        var completed = new DateTimeOffset(1909, 6, 1, 3, 0, 0, TimeSpan.Zero);
        var report = await SeedAsync(completed: completed);

        // 23:00 at UTC-5 is 04:00 UTC, an hour after the report finished.
        var found = await FindAsync(new DateTimeOffset(1909, 5, 31, 23, 0, 0, TimeSpan.FromHours(-5)), NoCap);

        Assert.Contains(found, r => r.ReportScheduleId == report);
    }

    [Fact]
    public async Task FindAsync_Selected_ReturnsTheFacility()
    {
        var cutoff = Cutoff(1910);
        var report = await SeedAsync(completed: cutoff.AddDays(-1));

        var found = await FindAsync(cutoff, NoCap);

        Assert.Equal(FacilityId, Assert.Single(found, r => r.ReportScheduleId == report).FacilityId);
    }

    [Fact]
    public async Task FindAsync_MoreThanTheCap_ReturnsTheOldestFirstUpToTheCap()
    {
        var cutoff = Cutoff(1850);
        var third = await SeedAsync(completed: cutoff.AddDays(-3));
        var oldest = await SeedAsync(completed: cutoff.AddDays(-30));
        await SeedAsync(completed: cutoff.AddDays(-1));
        var second = await SeedAsync(softDeleted: cutoff.AddDays(-10), isDeleted: true);
        await SeedAsync(completed: cutoff.AddDays(-2));

        var found = await FindAsync(cutoff, maxReports: 3);

        Assert.Equal([oldest, second, third], found.Select(r => r.ReportScheduleId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task FindAsync_CapBelowOne_Throws(int maxReports)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => FindAsync(Cutoff(1911), maxReports));
    }

    private static DateTimeOffset Cutoff(int year)
    {
        return new DateTimeOffset(year, 7, 1, 2, 0, 0, TimeSpan.Zero);
    }

    private async Task<IReadOnlyList<RetentionEligibleReport>> FindAsync(DateTimeOffset cutoff, int maxReports)
    {
        using var scope = _scopeFactory.CreateScope();
        var query = new RetentionEligibilityQuery(scope.ServiceProvider.GetRequiredService<ReportDbContext>());

        return await query.FindAsync(cutoff, maxReports);
    }

    private async Task<Guid> SeedAsync(DateTimeOffset? completed = null,
                                       DateTimeOffset? softDeleted = null,
                                       bool isDeleted = false,
                                       ReportRetentionStatus status = ReportRetentionStatus.None,
                                       DateTimeOffset? created = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        var anchor = completed ?? softDeleted ?? created ?? DateTimeOffset.UtcNow;
        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ReportStartDate = anchor.AddMonths(-2),
            ReportEndDate = anchor.AddMonths(-1),
            Frequency = Frequency.Monthly,
            Status = completed is null ? ScheduleStatus.Scheduled : ScheduleStatus.Submitted,
            CreateDate = (created ?? anchor).UtcDateTime,
            IsDeleted = isDeleted,
            CompletedDate = completed,
            SoftDeletedDate = softDeleted,
            RetentionStatus = status
        };

        context.ReportSchedule.Add(schedule);
        await context.SaveChangesAsync();

        return schedule.Id;
    }
}
