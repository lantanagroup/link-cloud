using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Domain;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Domain.Models;
using LantanaGroup.Link.Report.Services;
using LantanaGroup.Link.Shared.Application.Models.Retention;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using UnitTests.Shared.Auditing;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Report;

/// <summary>
/// Covers the scheduled retention check: re-sends first, one cutoff per run, cleanups recorded then published
/// then marked sent, batching, and how the run stops.
/// </summary>
[Trait("Category", "UnitTests")]
public class RetentionCheckTests
{
    private const string FacilityId = "facility-a";

    private static readonly DateTimeOffset FireTime = new(2026, 11, 6, 2, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = FireTime.AddSeconds(5);
    private static readonly DateTimeOffset ExpectedCutoff = new(2026, 8, 6, 2, 0, 5, TimeSpan.Zero);

    private readonly Mock<IRetentionPendingResender> _resender = new();
    private readonly Mock<IRetentionEligibilityQuery> _query = new();
    private readonly Mock<IRetentionCleanupManager> _manager = new();
    private readonly Mock<IRetentionCheckRequestedPublisher> _publisher = new();
    private readonly FakeTimeProvider _timeProvider = new(Now);
    private readonly CapturingLogger<RetentionCheck> _logger = new();
    private readonly List<string> _calls = [];

    public RetentionCheckTests()
    {
        _resender
            .Setup(r => r.ResendAsync(It.IsAny<RetentionCheckRun>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("resend"))
            .Returns(Task.CompletedTask);

        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<RetentionCheckRequestedValue>(), It.IsAny<CancellationToken>()))
            .Callback((RetentionCheckRequestedValue request, CancellationToken _) =>
                _calls.Add($"publish {request.RetentionCleanupId}"))
            .Returns(Task.CompletedTask);

        _manager
            .Setup(m => m.RecordSentAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback((Guid id, DateTimeOffset _, CancellationToken _) => _calls.Add($"sent {id}"))
            .ReturnsAsync(1);
    }

    [Fact]
    public async Task RunAsync_InvalidSettings_LogsErrorAndDoesNothing()
    {
        var check = CreateCheck(new Dictionary<string, string?> { ["Retention:Window"] = "P4Y" });

        await check.RunAsync(Run(), CancellationToken.None);

        var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("'P4Y' is 48 months", error.Message);
        _resender.VerifyNoOtherCalls();
        _query.VerifyNoOtherCalls();
        _manager.VerifyNoOtherCalls();
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_NothingEligible_ResendsAndCreatesNothing()
    {
        SetupBatches();

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find"], _calls);
        _manager.VerifyNoOtherCalls();
        var summary = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("0 cleanups requested for 0 reports", summary.Message);
    }

    [Fact]
    public async Task RunAsync_OneBatch_RecordsPublishesThenMarksSent()
    {
        var reports = Reports(2);
        SetupBatches(reports);
        var cleanupId = SetupCreatesCleanup();

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find", "create", $"publish {cleanupId}", $"sent {cleanupId}", "find"], _calls);
        _manager.Verify(m => m.RecordSentAsync(cleanupId, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_OneBatch_BuildsTheScheduledRequest()
    {
        var reports = Reports(2);
        SetupBatches(reports);
        SetupCreatesCleanup();
        RetentionCleanupRequest? captured = null;
        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback((RetentionCleanupRequest request, CancellationToken _) => captured = request)
            .ReturnsAsync((RetentionCleanupRequest request, CancellationToken _) => Created(Guid.NewGuid(), request));

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(RetentionRunIds.For(FireTime), captured.RunId);
        Assert.Equal(RetentionInitiatedBy.Scheduled, captured.InitiatedBy);
        Assert.Equal(RetentionMode.Scrub, captured.Mode);
        Assert.Equal("SystemUser", captured.InitiatedByUserId);
        Assert.Equal("SystemUser", captured.InitiatedByUserName);
        Assert.Equal(ExpectedCutoff, captured.CutoffDateTime);
        Assert.Equal(reports.Select(r => r.ReportScheduleId), captured.Reports.Select(r => r.ReportScheduleId));
        Assert.All(captured.Reports, r =>
        {
            Assert.Equal(FacilityId, r.FacilityId);
            Assert.Equal(ExpectedCutoff, r.CutoffDateTime);
            Assert.Equal(RetentionWindowUnit.Months, r.RetentionWindow!.Unit);
            Assert.Equal(3, r.RetentionWindow.Count);
        });
    }

    [Fact]
    public async Task RunAsync_OneBatch_AsksForTheCapWithTheCutoff()
    {
        SetupBatches();

        await CreateCheck(new Dictionary<string, string?> { ["Retention:MaxReportsPerCleanup"] = "7" })
            .RunAsync(Run(), CancellationToken.None);

        _query.Verify(q => q.FindAsync(ExpectedCutoff, 7, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_SeveralBatches_RequestsACleanupForEach()
    {
        SetupBatches(Reports(2), Reports(2), Reports(1));
        SetupCreatesCleanup();

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        _manager.Verify(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()),
                        Times.Exactly(3));
        _publisher.Verify(p => p.PublishAsync(It.IsAny<RetentionCheckRequestedValue>(),
                                              It.IsAny<CancellationToken>()),
                          Times.Exactly(3));
        var summary = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("3 cleanups requested for 5 reports", summary.Message);
    }

    [Fact]
    public async Task RunAsync_InvalidParticipants_StopsWithoutPublishing()
    {
        SetupBatches(Reports(2), Reports(2));
        var cleanupId = Guid.NewGuid();
        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("create"))
            .ReturnsAsync(new RetentionCleanupCreation
            {
                RetentionCleanupId = cleanupId,
                Status = RetentionCleanupStatus.Failed,
                FailureReason = "Retention:Participants is invalid."
            });

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find", "create"], _calls);
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(cleanupId.ToString(), warning.Message);
        _publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RunAsync_EveryReportAlreadyTaken_MovesOnWithoutPublishing()
    {
        var taken = Reports(2);
        SetupBatches(taken);
        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("create"))
            .ReturnsAsync(new RetentionCleanupCreation
            {
                SkippedReportScheduleIds = taken.Select(r => r.ReportScheduleId).ToList()
            });

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find", "create", "find"], _calls);
        _publisher.VerifyNoOtherCalls();
        var summary = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("2 reports skipped", summary.Message);
    }

    [Fact]
    public async Task RunAsync_SomeReportsAlreadyTaken_PublishesTheRestAndCountsBoth()
    {
        var batch = Reports(3);
        SetupBatches(batch);
        var cleanupId = Guid.NewGuid();
        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("create"))
            .ReturnsAsync((RetentionCleanupRequest request, CancellationToken _) =>
                Created(cleanupId, request) with
                {
                    IncludedReportScheduleIds = batch.Take(2).Select(r => r.ReportScheduleId).ToList(),
                    SkippedReportScheduleIds = [batch[2].ReportScheduleId]
                });

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find", "create", $"publish {cleanupId}", $"sent {cleanupId}", "find"], _calls);
        var summary = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information);
        Assert.Contains("1 cleanups requested for 2 reports; 1 reports skipped", summary.Message);
    }

    [Fact]
    public async Task RunAsync_MarkingSentFails_ThrowsAfterPublishing()
    {
        SetupBatches(Reports(1), Reports(1));
        var cleanupId = SetupCreatesCleanup();
        _manager
            .Setup(m => m.RecordSentAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unreachable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCheck().RunAsync(Run(),
                                                                                       CancellationToken.None));

        Assert.Equal(["resend", "find", "create", $"publish {cleanupId}"], _calls);
    }

    [Fact]
    public async Task RunAsync_ResendFails_LogsErrorAndStillSelects()
    {
        SetupBatches(Reports(1));
        var cleanupId = SetupCreatesCleanup();
        _resender
            .Setup(r => r.ResendAsync(It.IsAny<RetentionCheckRun>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resend broke"));

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("Re-sending unanswered retention requests failed", error.Message);
        Assert.Equal(["find", "create", $"publish {cleanupId}", $"sent {cleanupId}", "find"], _calls);
    }

    [Fact]
    public async Task RunAsync_CanceledDuringResend_StopsBeforeSelecting()
    {
        using var cancellation = new CancellationTokenSource();
        SetupBatches(Reports(1));
        _resender
            .Setup(r => r.ResendAsync(It.IsAny<RetentionCheckRun>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateCheck().RunAsync(Run(),
                                                                                          cancellation.Token));

        _query.VerifyNoOtherCalls();
        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public async Task RunAsync_SameReportSelectedTwice_StopsWithAnError()
    {
        var stuck = Reports(1);
        SetupBatches(stuck, stuck);
        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("create"))
            .ReturnsAsync(new RetentionCleanupCreation());

        await CreateCheck().RunAsync(Run(), CancellationToken.None);

        Assert.Equal(["resend", "find", "create", "find"], _calls);
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error && e.Message.Contains("selected again"));
    }

    [Fact]
    public async Task RunAsync_PublishFails_ThrowsWithoutMarkingSent()
    {
        SetupBatches(Reports(1));
        var cleanupId = SetupCreatesCleanup();
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<RetentionCheckRequestedValue>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCheck().RunAsync(Run(),
                                                                                       CancellationToken.None));

        _manager.Verify(m => m.RecordSentAsync(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
                                               It.IsAny<CancellationToken>()),
                        Times.Never);
        var error = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains(cleanupId.ToString(), error.Message);
    }

    [Fact]
    public async Task RunAsync_CanceledAfterAPublish_StopsBeforeTheNextBatch()
    {
        using var cancellation = new CancellationTokenSource();
        SetupBatches(Reports(1), Reports(1));
        SetupCreatesCleanup();
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<RetentionCheckRequestedValue>(), It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateCheck().RunAsync(Run(),
                                                                                          cancellation.Token));

        _query.Verify(q => q.FindAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                      Times.Once);
    }

    [Fact]
    public async Task RunAsync_Called_PassesTheTokenToEveryCall()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        SetupBatches(Reports(1));
        var cleanupId = SetupCreatesCleanup();

        await CreateCheck().RunAsync(Run(), token);

        _resender.Verify(r => r.ResendAsync(It.IsAny<RetentionCheckRun>(), token), Times.Once);
        _query.Verify(q => q.FindAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), token), Times.Exactly(2));
        _manager.Verify(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), token), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<RetentionCheckRequestedValue>(), token), Times.Once);
        _manager.Verify(m => m.RecordSentAsync(cleanupId, It.IsAny<DateTimeOffset>(), token), Times.Once);
    }

    private RetentionCheck CreateCheck(Dictionary<string, string?>? values = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values ?? [])
            .Build();

        return new RetentionCheck(RetentionCheckSettings.FromConfiguration(configuration),
                                  _resender.Object,
                                  _query.Object,
                                  _manager.Object,
                                  _publisher.Object,
                                  _timeProvider,
                                  _logger);
    }

    private static RetentionCheckRun Run()
    {
        return new RetentionCheckRun
        {
            ScheduledFireTimeUtc = FireTime,
            FireInstanceId = "fire-1"
        };
    }

    private static IReadOnlyList<RetentionEligibleReport> Reports(int count)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new RetentionEligibleReport(Guid.NewGuid(), FacilityId))
            .ToList();
    }

    private void SetupBatches(params IReadOnlyList<RetentionEligibleReport>[] batches)
    {
        var sequence = new Queue<IReadOnlyList<RetentionEligibleReport>>(batches);

        _query
            .Setup(q => q.FindAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("find"))
            .ReturnsAsync(() => sequence.Count > 0 ? sequence.Dequeue() : []);
    }

    private Guid SetupCreatesCleanup()
    {
        var cleanupId = Guid.NewGuid();

        _manager
            .Setup(m => m.CreateAsync(It.IsAny<RetentionCleanupRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("create"))
            .ReturnsAsync((RetentionCleanupRequest request, CancellationToken _) => Created(cleanupId, request));

        return cleanupId;
    }

    private static RetentionCleanupCreation Created(Guid cleanupId, RetentionCleanupRequest request)
    {
        return new RetentionCleanupCreation
        {
            RetentionCleanupId = cleanupId,
            Status = RetentionCleanupStatus.InProgress,
            IncludedReportScheduleIds = request.Reports.Select(r => r.ReportScheduleId).ToList(),
            Request = new RetentionCheckRequestedValue
            {
                RetentionCleanupId = cleanupId.ToString(),
                InitiatedBy = request.InitiatedBy,
                InitiatedDateTime = Now,
                Mode = request.Mode,
                CutoffDateTime = request.CutoffDateTime,
                Reports = request.Reports
                    .Select(r => new RetentionReport
                    {
                        FacilityId = r.FacilityId,
                        ReportScheduleId = r.ReportScheduleId.ToString(),
                        RetentionWindow = r.RetentionWindow,
                        CutoffDateTime = r.CutoffDateTime
                    })
                    .ToList()
            }
        };
    }
}
