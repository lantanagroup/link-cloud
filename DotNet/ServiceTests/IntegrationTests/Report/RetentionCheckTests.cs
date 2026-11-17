using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Data;
using LantanaGroup.Link.Report.Data.Entities;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report;

/// <summary>
/// Runs the scheduled retention check against real SQL Server, with the real eligibility query and cleanup
/// manager and a publisher that only records what it was given: batching under one run id, a replayed or
/// recovered run, a crash before publishing, an invalid participant list, and the next run after participants
/// have completed or failed a cleanup.
/// </summary>
/// <remarks>
/// The collection shares one database and the check reads every facility, so each test runs in its own year
/// long before any real data. The tests that claim their reports use the 1700s, older than anything another
/// test leaves at None. The invalid-participant test leaves its reports at None, so it uses 1920, after the
/// eligibility query tests' years, where no other test's cutoff reaches them.
/// </remarks>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class RetentionCheckTests
{
    private const string FacilityId = "retention-check-facility";

    private static readonly string[] DefaultServices = ["Report", "MeasureEval", "Validation", "DataAcquisition"];

    private readonly IServiceScopeFactory _scopeFactory;

    public RetentionCheckTests(ReportSqlServerIntegrationTestFixture fixture)
    {
        _scopeFactory = fixture.ScopeFactory;
    }

    [Fact]
    public async Task RunAsync_MoreReportsThanTheCap_RequestsOneCleanupPerBatchUnderOneRunId()
    {
        var now = Now(1701);
        var reports = await SeedReportsAsync(5, now.AddMonths(-4));
        var publisher = new RecordingPublisher();

        await RunAsync(FireTime(now), now, publisher, cap: 2);

        var cleanups = await LoadCleanupsAsync(reports);
        Assert.Equal(3, cleanups.Count);
        Assert.Equal([2, 2, 1], cleanups.Select(c => c.Reports.Count).OrderDescending());
        Assert.All(cleanups, c =>
        {
            Assert.Equal(RetentionRunIds.For(FireTime(now)), c.RunId);
            Assert.Equal(RetentionInitiatedBy.Scheduled, c.InitiatedBy);
            Assert.Equal(RetentionMode.Scrub, c.Mode);
            Assert.Equal("SystemUser", c.InitiatedByUserId);
            Assert.Equal(now.AddMonths(-3), c.CutoffDateTime);
        });

        Assert.Equal(cleanups.Select(c => c.Id.ToString()).Order(),
                     publisher.Published.Select(p => p.RetentionCleanupId).Order());
        Assert.All(await LoadResultsAsync(cleanups), r =>
        {
            Assert.Equal(1, r.SendCount);
            Assert.Equal(now, r.LastSentDateTime);
        });
        Assert.All(await LoadStatusesAsync(reports), s => Assert.Equal(ReportRetentionStatus.PendingDeletion, s));
    }

    [Fact]
    public async Task RunAsync_RunReplayedOrRecovered_RequestsNothingNew()
    {
        var now = Now(1711);
        var reports = await SeedReportsAsync(3, now.AddMonths(-4));
        var first = new RecordingPublisher();
        var replay = new RecordingPublisher();
        var recovery = new RecordingPublisher();

        // One firing run three times, as Quartz does when it replays or recovers it.
        await RunAsync(FireTime(now), now, first);
        await RunAsync(FireTime(now), now.AddMinutes(1), replay);
        await RunAsync(FireTime(now), now.AddMinutes(2), recovery, isRecovery: true);

        Assert.Single(first.Published);
        Assert.Empty(replay.Published);
        Assert.Empty(recovery.Published);
        var cleanup = Assert.Single(await LoadCleanupsAsync(reports));
        Assert.Equal(3, cleanup.Reports.Count);
        Assert.All(await LoadResultsAsync([cleanup]), r => Assert.Equal(1, r.SendCount));
    }

    [Fact]
    public async Task RunAsync_PublishFailedThenRecovered_LeavesTheCleanupUnsentAndCleansNothingTwice()
    {
        var now = Now(1721);
        var reports = await SeedReportsAsync(2, now.AddMonths(-4));
        var failing = new RecordingPublisher { Failure = new InvalidOperationException("broker unreachable") };
        var recovery = new RecordingPublisher();

        await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(FireTime(now), now, failing));
        await RunAsync(FireTime(now), now.AddMinutes(1), recovery, isRecovery: true);

        Assert.Empty(recovery.Published);
        var cleanup = Assert.Single(await LoadCleanupsAsync(reports));
        Assert.Equal(RetentionCleanupStatus.InProgress, cleanup.Status);
        Assert.All(await LoadResultsAsync([cleanup]), r =>
        {
            Assert.Equal(0, r.SendCount);
            Assert.Null(r.LastSentDateTime);
        });
        Assert.All(await LoadStatusesAsync(reports), s => Assert.Equal(ReportRetentionStatus.PendingDeletion, s));
    }

    [Fact]
    public async Task RunAsync_EveryParticipantCompleted_ScrubsAndTheNextRunSkipsThem()
    {
        var now = Now(1731);
        var reports = await SeedReportsAsync(2, now.AddMonths(-4));
        var first = new RecordingPublisher();
        var nextDay = new RecordingPublisher();

        await RunAsync(FireTime(now), now, first);
        var request = Assert.Single(first.Published);
        foreach (var service in DefaultServices)
        {
            await ApplyAsync(request, service, now.AddHours(1));
        }

        await RunAsync(FireTime(now).AddDays(1), now.AddDays(1), nextDay);

        Assert.Empty(nextDay.Published);
        var cleanup = Assert.Single(await LoadCleanupsAsync(reports));
        Assert.Equal(RetentionCleanupStatus.Completed, cleanup.Status);
        Assert.All(await LoadStatusesAsync(reports), s => Assert.Equal(ReportRetentionStatus.Scrubbed, s));
    }

    [Fact]
    public async Task RunAsync_OneParticipantFailedAReport_IsPartialAndTheNextRunSkipsBoth()
    {
        var now = Now(1741);
        var reports = await SeedReportsAsync(2, now.AddMonths(-4));
        var first = new RecordingPublisher();
        var nextDay = new RecordingPublisher();

        await RunAsync(FireTime(now), now, first);
        var request = Assert.Single(first.Published);
        var failedReport = reports[1].ToString();
        foreach (var service in DefaultServices)
        {
            var failed = service == "MeasureEval" ? failedReport : null;
            await ApplyAsync(request, service, now.AddHours(1), failed);
        }

        await RunAsync(FireTime(now).AddDays(1), now.AddDays(1), nextDay);

        Assert.Empty(nextDay.Published);
        var cleanup = Assert.Single(await LoadCleanupsAsync(reports));
        Assert.Equal(RetentionCleanupStatus.Partial, cleanup.Status);
        var statuses = await LoadStatusesByIdAsync(reports);
        Assert.Equal(ReportRetentionStatus.Scrubbed, statuses[reports[0]]);
        Assert.Equal(ReportRetentionStatus.PendingDeletion, statuses[reports[1]]);
    }

    [Fact]
    public async Task RunAsync_InvalidParticipants_RecordsOneFailedCleanupAndLeavesReportsNone()
    {
        var now = Now(1920);
        var reports = await SeedReportsAsync(2, now.AddMonths(-4));
        var publisher = new RecordingPublisher();
        var participants = RetentionParticipants.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:Participants:0:ServiceName"] = "Report"
            })
            .Build());

        // A large cap keeps these reports in the first batch alongside any older ones left at None.
        await RunAsync(FireTime(now), now, publisher, cap: 1000, participants: participants);

        Assert.Empty(publisher.Published);
        var cleanup = Assert.Single(await LoadCleanupsAsync(reports));
        Assert.Equal(RetentionCleanupStatus.Failed, cleanup.Status);
        Assert.NotNull(cleanup.FailureReason);
        Assert.All(await LoadStatusesAsync(reports), s => Assert.Equal(ReportRetentionStatus.None, s));
    }

    private static DateTimeOffset Now(int year)
    {
        return new DateTimeOffset(year, 6, 1, 2, 0, 5, TimeSpan.Zero);
    }

    private static DateTimeOffset FireTime(DateTimeOffset now)
    {
        return now.AddSeconds(-5);
    }

    private async Task RunAsync(DateTimeOffset fireTime,
                                DateTimeOffset now,
                                RecordingPublisher publisher,
                                int cap = 25,
                                bool isRecovery = false,
                                RetentionParticipants? participants = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var timeProvider = new FakeTimeProvider(now);
        var settings = RetentionCheckSettings.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Retention:MaxReportsPerCleanup"] = cap.ToString()
            })
            .Build());
        var manager = new RetentionCleanupManager(context,
                                                  participants ?? RetentionParticipants.FromConfiguration(
                                                      new ConfigurationBuilder().Build()),
                                                  timeProvider,
                                                  NullLogger<RetentionCleanupManager>.Instance);
        var check = new RetentionCheck(settings,
                                       new NoRetentionResends(NullLogger<NoRetentionResends>.Instance),
                                       new RetentionEligibilityQuery(context),
                                       manager,
                                       publisher,
                                       timeProvider,
                                       NullLogger<RetentionCheck>.Instance);

        var run = new RetentionCheckRun
        {
            ScheduledFireTimeUtc = fireTime,
            IsRecovery = isRecovery,
            FireInstanceId = Guid.NewGuid().ToString()
        };

        await check.RunAsync(run, CancellationToken.None);
    }

    // Replies to the request as a participant would: every report in it, Completed unless it is the one named
    // to fail.
    private async Task ApplyAsync(RetentionCheckRequestedValue request,
                                  string serviceName,
                                  DateTimeOffset now,
                                  string? failedReportScheduleId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var manager = new RetentionCleanupManager(scope.ServiceProvider.GetRequiredService<ReportDbContext>(),
                                                  RetentionParticipants.FromConfiguration(
                                                      new ConfigurationBuilder().Build()),
                                                  new FakeTimeProvider(now),
                                                  NullLogger<RetentionCleanupManager>.Instance);
        var completion = new RetentionCheckCompleteValue
        {
            RetentionCleanupId = request.RetentionCleanupId,
            ServiceName = serviceName,
            CompletedDateTime = now,
            Reports = request.Reports
                .Select(r => new RetentionReportResult
                {
                    ReportScheduleId = r.ReportScheduleId,
                    Status = r.ReportScheduleId == failedReportScheduleId
                        ? RetentionResultStatus.Failed
                        : RetentionResultStatus.Completed,
                    RecordsAffected = 1,
                    ErrorMessage = r.ReportScheduleId == failedReportScheduleId
                        ? "Delete failed (TimeoutException)."
                        : null,
                    CompletedDateTime = now
                })
                .ToList()
        };

        var outcome = await manager.ApplyCompletionAsync(Guid.Parse(request.RetentionCleanupId), completion);

        Assert.Equal(RetentionCompletionOutcome.Applied, outcome);
    }

    private async Task<List<Guid>> SeedReportsAsync(int count, DateTimeOffset completed)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var ids = new List<Guid>();

        for (var i = 0; i < count; i++)
        {
            var schedule = new ReportSchedule
            {
                Id = Guid.NewGuid(),
                FacilityId = FacilityId,
                ReportStartDate = completed.AddMonths(-2),
                ReportEndDate = completed.AddMonths(-1),
                Frequency = Frequency.Monthly,
                Status = ScheduleStatus.Submitted,
                CreateDate = DateTime.UtcNow,
                CompletedDate = completed.AddMinutes(i)
            };

            context.ReportSchedule.Add(schedule);
            ids.Add(schedule.Id);
        }

        await context.SaveChangesAsync();

        return ids;
    }

    private async Task<List<RetentionCleanup>> LoadCleanupsAsync(IReadOnlyCollection<Guid> reportIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        var cleanupIds = await context.RetentionCleanupReport
            .Where(r => reportIds.Contains(r.ReportScheduleId))
            .Select(r => r.RetentionCleanupId)
            .Distinct()
            .ToListAsync();

        return await context.RetentionCleanup
            .AsNoTracking()
            .Include(c => c.Reports)
            .Where(c => cleanupIds.Contains(c.Id))
            .ToListAsync();
    }

    private async Task<List<RetentionCleanupResult>> LoadResultsAsync(IEnumerable<RetentionCleanup> cleanups)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();
        var cleanupIds = cleanups.Select(c => c.Id).ToList();

        var results = await context.RetentionCleanupResult
            .AsNoTracking()
            .Where(r => cleanupIds.Contains(r.RetentionCleanupParticipant.RetentionCleanupId))
            .ToListAsync();

        Assert.NotEmpty(results);
        return results;
    }

    private async Task<List<ReportRetentionStatus>> LoadStatusesAsync(IReadOnlyCollection<Guid> reportIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        return await context.ReportSchedule
            .Where(r => reportIds.Contains(r.Id))
            .Select(r => r.RetentionStatus)
            .ToListAsync();
    }

    private async Task<Dictionary<Guid, ReportRetentionStatus>> LoadStatusesByIdAsync(
        IReadOnlyCollection<Guid> reportIds)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReportDbContext>();

        return await context.ReportSchedule
            .Where(r => reportIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.RetentionStatus);
    }

    private sealed class RecordingPublisher : IRetentionCheckRequestedPublisher
    {
        public List<RetentionCheckRequestedValue> Published { get; } = [];

        public Exception? Failure { get; init; }

        public Task PublishAsync(RetentionCheckRequestedValue request, CancellationToken cancellationToken)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Published.Add(request);
            return Task.CompletedTask;
        }
    }
}
