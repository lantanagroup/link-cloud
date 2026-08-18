using System.Diagnostics;
using Confluent.Kafka;
using LantanaGroup.Link.Report.Domain.Enums;
using LantanaGroup.Link.Report.Domain.Managers;
using LantanaGroup.Link.Report.Listeners;
using LantanaGroup.Link.Report.Models;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReportingStatus = LantanaGroup.Link.Report.Domain.Enums.ReportingStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.Report.Listeners;

/// <summary>
/// Performance regression tests for <see cref="GenerateReportListener"/>.
///
/// The GenerateReportRequested consumer has a ~5 minute Kafka poll timeout. Previously a
/// per-patient <c>IProducer.Flush()</c> inside <c>DataAcquisitionRequestedProducer.Produce</c>
/// (and a per-patient <c>await ProduceAsync(...)</c> in the Regenerate path of
/// <c>GenerateReportListener.ProcessMessageAsync</c>) serialized every Kafka round-trip,
/// which pushed 5000+ patient ad-hoc reports close to that timeout.
///
/// These tests lock in the optimization: Produce is now fire-and-forget and Flush is
/// called at most a handful of times per ad-hoc report.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
[Trait("Category", "Performance")]
public class GenerateReportListenerPerformanceTests
{
    private const int PatientCount = 5000;
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(30);

    private readonly ReportIntegrationTestFixture _fixture;

    public GenerateReportListenerPerformanceTests(ReportIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessMessageAsync_NewAdHoc_WithFiveThousandPatients_CompletesUnderThirtySeconds()
    {
        _fixture.DataAcquisitionRequestedKafkaProducerMock.Reset();

        var produceCallCount = 0;
        var flushCallCount = 0;

        _fixture.DataAcquisitionRequestedKafkaProducerMock
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, DataAcquisitionRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, DataAcquisitionRequestedValue>>>()))
            .Callback(() => Interlocked.Increment(ref produceCallCount));

        _fixture.DataAcquisitionRequestedKafkaProducerMock
            .Setup(p => p.Flush(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref flushCallCount));

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "perf-facility-adhoc-5k";
        var adhocReportId = Guid.NewGuid();
        var reportTypes = new List<string> { "DE-111", "DE-222" };
        var patientIds = Enumerable.Range(0, PatientCount)
            .Select(i => $"pat-{i:D6}")
            .ToList();

        var value = new GenerateReportValue
        {
            AdhocReportId = adhocReportId,
            StartDate = DateTime.UtcNow.AddDays(-30),
            EndDate = DateTime.UtcNow.AddDays(30),
            ReportTypes = reportTypes,
            PatientIds = patientIds,
            BypassSubmission = false,
            Regenerate = false
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        var stopwatch = Stopwatch.StartNew();
        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < MaxDuration,
            $"Ad-hoc report for {PatientCount} patients took {stopwatch.Elapsed.TotalSeconds:F2}s " +
            $"(budget: {MaxDuration.TotalSeconds}s).");

        var entries = await reportEntryManager.FindAsync(e => e.ReportScheduleId == adhocReportId);
        Assert.Equal(PatientCount, entries.Count);

        Assert.Equal(PatientCount, produceCallCount);

        // Core optimization: ProcessMessageAsync must Flush the producer exactly once
        // per invocation (never once-per-patient, which was the original bottleneck).
        Assert.Equal(1, flushCallCount);
    }

    [Fact]
    public async Task ProcessMessageAsync_Regenerate_WithFiveThousandPatients_CompletesUnderThirtySeconds()
    {
        _fixture.EvaluationRequestedProducerMock.Reset();

        var produceCallCount = 0;
        var produceAsyncCallCount = 0;
        var flushCallCount = 0;

        _fixture.EvaluationRequestedProducerMock
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, EvaluationRequestedValue>>(),
                It.IsAny<Action<DeliveryReport<string, EvaluationRequestedValue>>>()))
            .Callback(() => Interlocked.Increment(ref produceCallCount));

        _fixture.EvaluationRequestedProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, EvaluationRequestedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref produceAsyncCallCount))
            .ReturnsAsync(new DeliveryResult<string, EvaluationRequestedValue>());

        _fixture.EvaluationRequestedProducerMock
            .Setup(p => p.Flush(It.IsAny<CancellationToken>()))
            .Callback(() => Interlocked.Increment(ref flushCallCount));

        using var scope = _fixture.ScopeFactory.CreateScope();
        var listener = scope.ServiceProvider.GetRequiredService<GenerateReportListener>();
        var reportScheduledManager = scope.ServiceProvider.GetRequiredService<IReportScheduledManager>();
        var reportEntryManager = scope.ServiceProvider.GetRequiredService<IReportEntryManager>();

        var facilityId = "perf-facility-regen-5k";
        var originalReportId = Guid.NewGuid();
        var newAdhocReportId = Guid.NewGuid();
        var reportTypes = new List<string> { "DE-111" };

        var existingSchedule = new ReportScheduleModel
        {
            Id = originalReportId,
            FacilityId = facilityId,
            ReportStartDate = DateTimeOffset.UtcNow.AddDays(-60),
            ReportEndDate = DateTimeOffset.UtcNow.AddDays(-30),
            Frequency = Frequency.Monthly,
            ReportTypes = reportTypes,
            Status = ScheduleStatus.Scheduled,
            CreateDate = DateTime.UtcNow
        };
        await reportScheduledManager.AddAsync(existingSchedule, CancellationToken.None);

        var existingEntries = Enumerable.Range(0, PatientCount).Select(i => new ReportEntryModel
        {
            PatientId = $"regen-pat-{i:D6}",
            ReportScheduleId = originalReportId,
            FacilityId = facilityId,
            ReportingStatus = ReportingStatus.PatientIdentified,
            CreateDate = DateTime.UtcNow,
            MeasureReports = new List<EntryMeasureReportModel>
            {
                new EntryMeasureReportModel
                {
                    ReportType = "DE-111",
                    Status = MeasureReportStatus.ReadyForValidation
                }
            }
        }).ToList();
        await reportEntryManager.AddRangeAsync(existingEntries, CancellationToken.None);

        var value = new GenerateReportValue
        {
            ReportId = originalReportId,
            AdhocReportId = newAdhocReportId,
            Regenerate = true,
            BypassSubmission = false
        };

        var consumeResult = new ConsumeResult<string, GenerateReportValue>
        {
            Message = new Message<string, GenerateReportValue>
            {
                Key = facilityId,
                Value = value
            }
        };

        var stopwatch = Stopwatch.StartNew();
        await listener.ProcessMessageAsync(consumeResult, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < MaxDuration,
            $"Regenerate report for {PatientCount} patients took {stopwatch.Elapsed.TotalSeconds:F2}s " +
            $"(budget: {MaxDuration.TotalSeconds}s).");

        var newEntries = await reportEntryManager.FindAsync(e => e.ReportScheduleId == newAdhocReportId);
        Assert.Equal(PatientCount, newEntries.Count);

        // Regenerate path must use fire-and-forget Produce rather than awaited
        // ProduceAsync, and must Flush exactly once per invocation (never per-patient).
        Assert.Equal(PatientCount, produceCallCount);
        Assert.Equal(0, produceAsyncCallCount);
        Assert.Equal(1, flushCallCount);
    }
}
