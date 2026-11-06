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

    [Fact]
    public async Task ApplyCompletionAsync_EveryParticipantCompletes_CompletesAndScrubs()
    {
        var first = await SeedScheduleAsync();
        var second = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(first, second), DefaultParticipants()))
            .RetentionCleanupId!.Value;

        foreach (var service in new[] { "Report", "MeasureEval", "Validation", "DataAcquisition" })
        {
            Assert.Equal(RetentionCompletionOutcome.Applied,
                         await ApplyAsync(cleanupId, ReportCompletion(cleanupId, service, Completed(first, 4),
                                                                      Completed(second, 6))));
        }

        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.Completed, cleanup.Status);
        Assert.Equal(Now, cleanup.CompletedDateTime);
        Assert.All(cleanup.Participants.SelectMany(p => p.Results),
                   r => Assert.Equal(RetentionCleanupResultStatus.Completed, r.Status));
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(first));
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(second));
    }

    [Fact]
    public async Task ApplyCompletionAsync_OneReportFails_ScrubsOnlyTheOtherAndIsPartial()
    {
        var done = await SeedScheduleAsync();
        var failing = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(done, failing), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Report", Completed(done, 1), Completed(failing, 1)));
        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "MeasureEval", Completed(done, 2),
                                                     Failed(failing, 1, "Bulk delete failed (MongoException).")));

        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.Partial, cleanup.Status);
        Assert.Null(cleanup.CompletedDateTime);
        var failed = Assert.Single(cleanup.Participants.SelectMany(p => p.Results),
                                   r => r.Status == RetentionCleanupResultStatus.Failed);
        Assert.Equal("Bulk delete failed (MongoException).", failed.ErrorMessage);
        Assert.Equal(1, failed.RecordsAffected);
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(done));
        Assert.Equal(ReportRetentionStatus.PendingDeletion, await RetentionStatusAsync(failing));
    }

    [Fact]
    public async Task ApplyCompletionAsync_FirstFailureArrives_ShowsFailedWithoutWaiting()
    {
        var report = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(report), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "MeasureEval", Failed(report, 0, "Timed out.")));

        Assert.Equal(RetentionCleanupStatus.Failed, (await LoadCleanupAsync(cleanupId)).Status);
        Assert.Equal(ReportRetentionStatus.PendingDeletion, await RetentionStatusAsync(report));
    }

    [Fact]
    public async Task ApplyCompletionAsync_LateSuccessAfterFailure_MovesTheCleanupForward()
    {
        var report = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(report), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "MeasureEval", Failed(report, 0, "Timed out.")));
        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Report", Completed(report, 3)));
        Assert.Equal(RetentionCleanupStatus.Partial, (await LoadCleanupAsync(cleanupId)).Status);

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "MeasureEval", Completed(report, 9)));

        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.Completed, cleanup.Status);
        var measureEval = cleanup.Participants.Single(p => p.ServiceName == "MeasureEval").Results.Single();
        Assert.Null(measureEval.ErrorMessage);
        Assert.Equal(9, measureEval.RecordsAffected);
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(report));
    }

    [Fact]
    public async Task ApplyCompletionAsync_ReportMissingFromCompletion_RecordsItFailed()
    {
        var listed = await SeedScheduleAsync();
        var omitted = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(listed, omitted), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "MeasureEval", Completed(listed, 1)));

        var results = (await LoadCleanupAsync(cleanupId)).Participants
            .Single(p => p.ServiceName == "MeasureEval")
            .Results
            .ToList();
        var missing = Assert.Single(results, r => r.Status == RetentionCleanupResultStatus.Failed);
        Assert.Contains("Missing from the MeasureEval completion", missing.ErrorMessage);
        Assert.Equal(0, missing.RecordsAffected);
    }

    [Fact]
    public async Task ApplyCompletionAsync_DuplicateCompletion_ChangesNothing()
    {
        var report = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(report), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;
        var completion = ReportCompletion(cleanupId, "MeasureEval", Completed(report, 5));

        await ApplyAsync(cleanupId, completion);
        var before = await LoadCleanupAsync(cleanupId);
        var rerun = ReportCompletion(cleanupId, "MeasureEval", Completed(report, 0, Now.AddHours(1)));
        await ApplyAsync(cleanupId, completion);
        await ApplyAsync(cleanupId, rerun);

        var after = await LoadCleanupAsync(cleanupId);
        Assert.Equal(before.Status, after.Status);
        var result = after.Participants.Single(p => p.ServiceName == "MeasureEval").Results.Single();
        Assert.Equal(5, result.RecordsAffected);
        Assert.Equal(Now, result.CompletedDateTime);
        Assert.Equal(2, after.Participants.SelectMany(p => p.Results).Count());
    }

    [Fact]
    public async Task ApplyCompletionAsync_UnexpectedService_ChangesNothing()
    {
        var report = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(report), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;

        var outcome = await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Census", Failed(report, 0, "x")));

        Assert.Equal(RetentionCompletionOutcome.UnexpectedService, outcome);
        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.InProgress, cleanup.Status);
        Assert.All(cleanup.Participants.SelectMany(p => p.Results),
                   r => Assert.Equal(RetentionCleanupResultStatus.Pending, r.Status));
    }

    [Fact]
    public async Task ApplyCompletionAsync_UnknownCleanup_ReturnsUnknown()
    {
        var cleanupId = Guid.NewGuid();

        var outcome = await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Report"));

        Assert.Equal(RetentionCompletionOutcome.UnknownCleanup, outcome);
    }

    [Fact]
    public async Task ApplyCompletionAsync_CutoffScopedParticipant_NeverHoldsBackScrubbed()
    {
        var report = await SeedScheduleAsync();
        var participants = Participants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = "Report",
            ["Retention:Participants:1:ServiceName"] = "Audit",
            ["Retention:Participants:1:Scope"] = "Cutoff"
        });
        var cleanupId = (await CreateAsync(ScheduledRequest(report), participants)).RetentionCleanupId!.Value;

        await ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Report", Completed(report, 2)));

        Assert.Equal(RetentionCleanupStatus.InProgress, (await LoadCleanupAsync(cleanupId)).Status);
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(report));

        await ApplyAsync(cleanupId, new RetentionCheckCompleteValue
        {
            RetentionCleanupId = cleanupId.ToString(),
            ServiceName = "Audit",
            CompletedDateTime = Now,
            Summary = new RetentionReportResult
            {
                Status = RetentionResultStatus.Completed,
                RecordsAffected = 40,
                CompletedDateTime = Now
            }
        });

        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.Completed, cleanup.Status);
        Assert.Equal(40, cleanup.Participants.Single(p => p.ServiceName == "Audit").Results.Single().RecordsAffected);
    }

    [Fact]
    public async Task ApplyCompletionAsync_OnlyCutoffScopedParticipants_ScrubsTheReports()
    {
        var first = await SeedScheduleAsync();
        var second = await SeedScheduleAsync();
        var participants = Participants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Audit",
            ["Retention:Participants:0:Scope"] = "Cutoff"
        });
        var cleanupId = (await CreateAsync(ScheduledRequest(first, second), participants))
            .RetentionCleanupId!.Value;

        var outcome = await ApplyAsync(cleanupId, new RetentionCheckCompleteValue
        {
            RetentionCleanupId = cleanupId.ToString(),
            ServiceName = "Audit",
            CompletedDateTime = Now,
            Summary = new RetentionReportResult
            {
                Status = RetentionResultStatus.Completed,
                RecordsAffected = 40,
                CompletedDateTime = Now
            }
        });

        Assert.Equal(RetentionCompletionOutcome.Applied, outcome);
        Assert.Equal(RetentionCleanupStatus.Completed, (await LoadCleanupAsync(cleanupId)).Status);
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(first));
        Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(second));
    }

    [Fact]
    public async Task ApplyCompletionAsync_UndefinedStatus_ThrowsAndChangesNothing()
    {
        var report = await SeedScheduleAsync();
        var cleanupId = (await CreateAsync(ScheduledRequest(report), ReportAndMeasureEval()))
            .RetentionCleanupId!.Value;
        var reply = Completed(report, 3);
        reply.Status = (RetentionResultStatus)2;

        await Assert.ThrowsAsync<ArgumentException>(
            () => ApplyAsync(cleanupId, ReportCompletion(cleanupId, "Report", reply)));

        var cleanup = await LoadCleanupAsync(cleanupId);
        Assert.Equal(RetentionCleanupStatus.InProgress, cleanup.Status);
        Assert.All(cleanup.Participants.SelectMany(p => p.Results),
                   r => Assert.Equal(RetentionCleanupResultStatus.Pending, r.Status));
        Assert.Equal(ReportRetentionStatus.PendingDeletion, await RetentionStatusAsync(report));
    }

    [Fact]
    public async Task ApplyCompletionAsync_ParticipantsReplyConcurrently_EndsCompleted()
    {
        var reports = new List<Guid>();
        for (var i = 0; i < 10; i++)
        {
            reports.Add(await SeedScheduleAsync());
        }

        var cleanupId = (await CreateAsync(ScheduledRequest([.. reports]), DefaultParticipants()))
            .RetentionCleanupId!.Value;
        var replies = reports.Select(id => Completed(id, 1)).ToArray();

        await Task.WhenAll(new[] { "Report", "MeasureEval", "Validation", "DataAcquisition" }
            .Select(service => ApplyAsync(cleanupId, ReportCompletion(cleanupId, service, replies))));

        Assert.Equal(RetentionCleanupStatus.Completed, (await LoadCleanupAsync(cleanupId)).Status);
        foreach (var report in reports)
        {
            Assert.Equal(ReportRetentionStatus.Scrubbed, await RetentionStatusAsync(report));
        }
    }

    private async Task<RetentionCompletionOutcome> ApplyAsync(Guid retentionCleanupId,
                                                              RetentionCheckCompleteValue completion)
    {
        using var scope = _scopeFactory.CreateScope();
        return await CreateManager(scope, DefaultParticipants()).ApplyCompletionAsync(retentionCleanupId,
                                                                                       completion);
    }

    private static RetentionCheckCompleteValue ReportCompletion(Guid retentionCleanupId,
                                                                string serviceName,
                                                                params RetentionReportResult[] reports)
    {
        return new RetentionCheckCompleteValue
        {
            RetentionCleanupId = retentionCleanupId.ToString(),
            ServiceName = serviceName,
            CompletedDateTime = Now,
            Reports = [.. reports]
        };
    }

    private static RetentionReportResult Completed(Guid reportScheduleId,
                                                   long recordsAffected,
                                                   DateTimeOffset? completedDateTime = null)
    {
        return new RetentionReportResult
        {
            ReportScheduleId = reportScheduleId.ToString(),
            Status = RetentionResultStatus.Completed,
            RecordsAffected = recordsAffected,
            CompletedDateTime = completedDateTime ?? Now
        };
    }

    private static RetentionReportResult Failed(Guid reportScheduleId, long recordsAffected, string errorMessage)
    {
        return new RetentionReportResult
        {
            ReportScheduleId = reportScheduleId.ToString(),
            Status = RetentionResultStatus.Failed,
            RecordsAffected = recordsAffected,
            ErrorMessage = errorMessage,
            CompletedDateTime = Now
        };
    }

    private static RetentionParticipants ReportAndMeasureEval()
    {
        return Participants(new Dictionary<string, string?>
        {
            ["Retention:Participants:0:ServiceName"] = "Report",
            ["Retention:Participants:0:Scope"] = "Report",
            ["Retention:Participants:1:ServiceName"] = "MeasureEval",
            ["Retention:Participants:1:Scope"] = "Report"
        });
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
