using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Managers;

/// <summary>
/// Covers recording a retention cleanup on real SQL Server: what one cleanup writes, the failed cleanup an
/// invalid participant list produces, and that a report can be taken by only one cleanup.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class RetentionCleanupManagerTests
{
    private const string FacilityId = "retention-facility";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = new(2024, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IServiceScopeFactory _scopeFactory;

    public RetentionCleanupManagerTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task CreateAsync_DefaultParticipants_RecordsEverythingAndTakesTheReports()
    {
        var first = await SeedScheduleAsync();
        var second = await SeedScheduleAsync();
        var request = ScheduledRequest(first, second);

        var creation = await CreateAsync(request, DefaultParticipants());

        Assert.Equal(RetentionCleanupStatus.InProgress, creation.Status);
        Assert.Equal(new[] { first, second }.Order(), creation.IncludedReportScheduleIds.Order());
        Assert.Empty(creation.SkippedReportScheduleIds);

        var cleanup = await LoadCleanupAsync(creation.RetentionCleanupId!.Value);
        Assert.Equal(request.RunId, cleanup.RunId);
        Assert.Equal(RetentionInitiatedBy.Scheduled, cleanup.InitiatedBy);
        Assert.Equal(Now, cleanup.InitiatedDateTime);
        Assert.Equal(Cutoff, cleanup.CutoffDateTime);
        Assert.Null(cleanup.FailureReason);
        Assert.Equal(2, cleanup.Reports.Count);
        Assert.All(cleanup.Reports, r => Assert.Equal(RetentionWindowUnit.Months, r.RetentionWindowUnit));

        Assert.Equal(["DataAcquisition", "MeasureEval", "Report", "Validation"],
                     cleanup.Participants.Select(p => p.ServiceName).Order());
        var results = cleanup.Participants.SelectMany(p => p.Results).ToList();
        Assert.Equal(8, results.Count);
        Assert.All(results, r =>
        {
            Assert.Equal(RetentionCleanupResultStatus.Pending, r.Status);
            Assert.NotNull(r.RetentionCleanupReportId);
            Assert.Equal(0, r.SendCount);
            Assert.Null(r.LastSentDateTime);
        });

        Assert.Equal(ReportRetentionStatus.PendingDeletion, await RetentionStatusAsync(first));
        Assert.Equal(ReportRetentionStatus.PendingDeletion, await RetentionStatusAsync(second));
    }

    [Fact]
    public async Task CreateAsync_DefaultParticipants_ReturnsTheRequestToPublish()
    {
        var report = await SeedScheduleAsync();

        var creation = await CreateAsync(ScheduledRequest(report), DefaultParticipants());

        var message = Assert.IsType<RetentionCheckRequestedValue>(creation.Request);
        Assert.Equal(creation.RetentionCleanupId.ToString(), message.RetentionCleanupId);
        Assert.Equal(RetentionMode.Scrub, message.Mode);
        Assert.Equal(Now, message.InitiatedDateTime);
        Assert.Null(message.Participants);
        var sent = Assert.Single(message.Reports);
        Assert.Equal(report.ToString(), sent.ReportScheduleId);
        Assert.Equal(FacilityId, sent.FacilityId);
        Assert.Equal(RetentionWindowUnit.Months, sent.RetentionWindow!.Unit);
        Assert.Equal(24, sent.RetentionWindow.Count);
    }

    [Fact]
    public async Task CreateAsync_CutoffScopedParticipant_GetsOneResultWithNoReport()
    {
        var first = await SeedScheduleAsync();
        var second = await SeedScheduleAsync();
        var participants = Participants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = "Report",
            ["Retention:Participants:1:ServiceName"] = "Audit",
            ["Retention:Participants:1:Scope"] = "Cutoff"
        });

        var creation = await CreateAsync(ScheduledRequest(first, second), participants);

        var cleanup = await LoadCleanupAsync(creation.RetentionCleanupId!.Value);
        var audit = Assert.Single(cleanup.Participants, p => p.ServiceName == "Audit");
        Assert.Equal(RetentionParticipantScope.Cutoff, audit.Scope);
        Assert.Null(Assert.Single(audit.Results).RetentionCleanupReportId);
        Assert.Equal(2, cleanup.Participants.Single(p => p.ServiceName == "Report").Results.Count);
    }

    [Fact]
    public async Task CreateAsync_InvalidParticipants_RecordsFailedAndLeavesTheReportsNone()
    {
        var report = await SeedScheduleAsync();
        var participants = Participants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report"
        });

        var creation = await CreateAsync(ScheduledRequest(report), participants);

        Assert.Equal(RetentionCleanupStatus.Failed, creation.Status);
        Assert.Null(creation.Request);
        Assert.Contains("has no Scope", creation.FailureReason);
        Assert.Equal([report], creation.IncludedReportScheduleIds);

        var cleanup = await LoadCleanupAsync(creation.RetentionCleanupId!.Value);
        Assert.Equal(RetentionCleanupStatus.Failed, cleanup.Status);
        Assert.Single(cleanup.Reports);
        Assert.Empty(cleanup.Participants);
        Assert.Equal(ReportRetentionStatus.None, await RetentionStatusAsync(report));
    }

    [Fact]
    public async Task CreateAsync_SomeReportsNotNone_TakesOnlyTheRest()
    {
        var available = await SeedScheduleAsync();
        var pending = await SeedScheduleAsync(ReportRetentionStatus.PendingDeletion);
        var scrubbed = await SeedScheduleAsync(ReportRetentionStatus.Scrubbed);
        var missing = Guid.NewGuid();

        var creation = await CreateAsync(ScheduledRequest(available, pending, scrubbed, missing),
                                         DefaultParticipants());

        Assert.Equal([available], creation.IncludedReportScheduleIds);
        Assert.Equal(new[] { pending, scrubbed, missing }.Order(), creation.SkippedReportScheduleIds.Order());
        Assert.Equal(available.ToString(), Assert.Single(creation.Request!.Reports).ReportScheduleId);
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(scrubbed));
    }

    [Fact]
    public async Task CreateAsync_NoReportStillNone_RecordsNothing()
    {
        var pending = await SeedScheduleAsync(ReportRetentionStatus.PendingDeletion);
        var request = ScheduledRequest(pending);

        var creation = await CreateAsync(request, DefaultParticipants());

        Assert.Null(creation.RetentionCleanupId);
        Assert.Null(creation.Status);
        Assert.Null(creation.Request);
        Assert.Equal([pending], creation.SkippedReportScheduleIds);

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        Assert.False(await context.RetentionCleanup.AnyAsync(c => c.RunId == request.RunId));
    }

    [Fact]
    public async Task CreateAsync_TwoConcurrentCleanupsForTheSameReports_EachReportIsTakenOnce()
    {
        var reports = new List<Guid>();
        for (var i = 0; i < 20; i++)
        {
            reports.Add(await SeedScheduleAsync());
        }

        var creations = await Task.WhenAll(
            CreateAsync(ScheduledRequest([.. reports]), DefaultParticipants()),
            CreateAsync(ScheduledRequest([.. reports]), DefaultParticipants()));

        var taken = creations.SelectMany(c => c.IncludedReportScheduleIds).ToList();
        Assert.Equal(reports.Order(), taken.Order());

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var rows = await context.RetentionCleanupReport
            .Where(r => reports.Contains(r.ReportScheduleId))
            .GroupBy(r => r.ReportScheduleId)
            .Select(g => g.Count())
            .ToListAsync();
        Assert.Equal(20, rows.Count);
        Assert.All(rows, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task RecordSentAsync_CalledTwice_StampsAndCountsEveryResult()
    {
        var report = await SeedScheduleAsync();
        var creation = await CreateAsync(ScheduledRequest(report), DefaultParticipants());
        var cleanupId = creation.RetentionCleanupId!.Value;
        var firstSend = Now.AddSeconds(1);
        var secondSend = Now.AddDays(2);

        using (var scope = _scopeFactory.CreateScope())
        {
            var manager = CreateManager(scope, DefaultParticipants());
            Assert.Equal(4, await manager.RecordSentAsync(cleanupId, firstSend));
            Assert.Equal(4, await manager.RecordSentAsync(cleanupId, secondSend));
        }

        var results = (await LoadCleanupAsync(cleanupId)).Participants.SelectMany(p => p.Results).ToList();
        Assert.All(results, r =>
        {
            Assert.Equal(2, r.SendCount);
            Assert.Equal(secondSend, r.LastSentDateTime);
        });
    }

    [Fact]
    public async Task CreateAsync_SingleReportPurge_RecordsWithoutAWindow()
    {
        var report = await SeedScheduleAsync();
        var request = ScheduledRequest(report) with
        {
            InitiatedBy = RetentionInitiatedBy.SingleReport,
            Mode = RetentionMode.Purge,
            InitiatedByUserId = "user-1",
            InitiatedByUserName = "A User",
            Reports = [new RetentionCleanupRequestReport
            {
                ReportScheduleId = report,
                FacilityId = FacilityId,
                CutoffDateTime = Cutoff
            }]
        };

        var creation = await CreateAsync(request, DefaultParticipants());

        Assert.Equal(RetentionMode.Purge, creation.Request!.Mode);
        Assert.Null(Assert.Single(creation.Request.Reports).RetentionWindow);
        var stored = Assert.Single((await LoadCleanupAsync(creation.RetentionCleanupId!.Value)).Reports);
        Assert.Null(stored.RetentionWindowUnit);
        Assert.Null(stored.RetentionWindowCount);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("duplicate")]
    [InlineData("purge-scheduled")]
    [InlineData("single-two-reports")]
    [InlineData("single-with-window")]
    [InlineData("scheduled-without-window")]
    [InlineData("no-user")]
    public async Task CreateAsync_RequestBreaksTheContract_Throws(string breach)
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();
        var windowless = new RetentionCleanupRequestReport
        {
            ReportScheduleId = id,
            FacilityId = FacilityId,
            CutoffDateTime = Cutoff
        };
        var single = ScheduledRequest(id) with { InitiatedBy = RetentionInitiatedBy.SingleReport };
        var request = breach switch
        {
            "empty" => ScheduledRequest() with { Reports = [] },
            "duplicate" => ScheduledRequest(id, id),
            "purge-scheduled" => ScheduledRequest(id) with { Mode = RetentionMode.Purge },
            "single-two-reports" => single with { Reports = [windowless, windowless with { ReportScheduleId = other }] },
            "single-with-window" => single,
            "scheduled-without-window" => ScheduledRequest(id) with { Reports = [windowless] },
            "no-user" => ScheduledRequest(id) with { InitiatedByUserName = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(breach))
        };

        await Assert.ThrowsAsync<ArgumentException>(() => CreateAsync(request, DefaultParticipants()));
    }

    private async Task<RetentionCleanupCreation> CreateAsync(RetentionCleanupRequest request,
                                                             RetentionParticipants participants)
    {
        using var scope = _scopeFactory.CreateScope();
        return await CreateManager(scope, participants).CreateAsync(request);
    }

    private static RetentionCleanupManager CreateManager(IServiceScope scope,
                                                         RetentionParticipants participants)
    {
        return new RetentionCleanupManager(scope.ServiceProvider.GetRequiredService<ReportDbContext>(),
                                           participants,
                                           new FakeTimeProvider(Now),
                                           NullLogger<RetentionCleanupManager>.Instance);
    }

    private static RetentionCleanupRequest ScheduledRequest(params Guid[] reportScheduleIds)
    {
        return new RetentionCleanupRequest
        {
            RunId = Guid.NewGuid(),
            InitiatedBy = RetentionInitiatedBy.Scheduled,
            InitiatedByUserId = "SystemUser",
            InitiatedByUserName = "SystemUser",
            Mode = RetentionMode.Scrub,
            CutoffDateTime = Cutoff,
            Reports = reportScheduleIds
                .Select(id => new RetentionCleanupRequestReport
                {
                    ReportScheduleId = id,
                    FacilityId = FacilityId,
                    RetentionWindow = new RetentionWindow { Unit = RetentionWindowUnit.Months, Count = 24 },
                    CutoffDateTime = Cutoff
                })
                .ToList()
        };
    }

    private static RetentionParticipants DefaultParticipants() => Participants(new Dictionary<string, string?>());

    private static RetentionParticipants Participants(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return RetentionParticipants.FromConfiguration(configuration);
    }

    private async Task<Guid> SeedScheduleAsync(ReportRetentionStatus status = ReportRetentionStatus.None)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            FacilityId = FacilityId,
            ReportStartDate = Cutoff.AddMonths(-2),
            ReportEndDate = Cutoff.AddMonths(-1),
            Frequency = Frequency.Monthly,
            Status = ScheduleStatus.Submitted,
            CreateDate = DateTime.UtcNow,
            RetentionStatus = status
        };

        context.ReportSchedule.Add(schedule);
        await context.SaveChangesAsync();

        return schedule.Id;
    }

    private async Task<RetentionCleanup> LoadCleanupAsync(Guid retentionCleanupId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        return await context.RetentionCleanup
            .AsNoTracking()
            .Include(c => c.Reports)
            .Include(c => c.Participants)
            .ThenInclude(p => p.Results)
            .SingleAsync(c => c.Id == retentionCleanupId);
    }

    private async Task<ReportRetentionStatus> RetentionStatusAsync(Guid reportScheduleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        return await context.ReportSchedule
            .Where(r => r.Id == reportScheduleId)
            .Select(r => r.RetentionStatus)
            .SingleAsync();
    }
}
