using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AcquisitionProcessingJobTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;
    private readonly IOptions<AcquisitionWorkerProcessorSettings> _settings;

    public AcquisitionProcessingJobTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
        _settings = Options.Create(new AcquisitionWorkerProcessorSettings());
    }

    [Fact]
    public async Task ProcessPendingLogs_WithValidConfigAndWithinWindow_ProducesMessagesAndUpdatesStatus()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var createLog = new CreateDataAcquisitionLogModel
        {
            FacilityId = facilityId,
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            PatientId = "Patient/123",
            ScheduledReport = new ScheduledReport {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };

        var log = await logManager.CreateAsync(createLog);

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_NoConfig_SetsMaxRetriesReachedWithNoteImmediately()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var missingConfigFacilityId = $"MissingConfigFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();



        var log = new DataAcquisitionLog
        {
            FacilityId = missingConfigFacilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId == missingConfigFacilityId),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog.Status);

        var note = await assertDbContext.DataAcquisitionLogNotes
            .FirstOrDefaultAsync(n => n.DataAcquisitionLogId == log.Id);
        Assert.NotNull(note);
        Assert.Contains("FhirQueryConfiguration", note.Note);
    }

    [Fact]
    public async Task ProcessPendingLogs_NoConfig_AlreadyFailedLog_AlsoSetsMaxRetriesReachedWithNote()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var missingConfigFacilityId = $"MissingConfigFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // A log already in the Failed state (e.g. from a previous run under the old logic).
        var failedLog = new DataAcquisitionLog
        {
            FacilityId = missingConfigFacilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 3,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/456",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(failedLog);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(failedLog.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog.Status);

        var note = await assertDbContext.DataAcquisitionLogNotes
            .FirstOrDefaultAsync(n => n.DataAcquisitionLogId == failedLog.Id);
        Assert.NotNull(note);
        Assert.Contains("FhirQueryConfiguration", note.Note);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideAcquisitionWindow_SkipsProcessing()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = DateTime.UtcNow.AddHours(2).TimeOfDay,
            MaxAcquisitionPullTime = DateTime.UtcNow.AddHours(3).TimeOfDay
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_FailedWithRetries_RetriesUpToMax()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId1 = $"TestReportId_1_{testTag}";
        var reportTrackingId2 = $"TestReportId_2_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = DataAcquisitionLog.MaxRetryAttempts - 1,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId1,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId1,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = DataAcquisitionLog.MaxRetryAttempts,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId2,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId2,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log1.Id),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log2.Id),
                It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog1.Status);
        // Note: The bulk update in AcquisitionProcessingJob.ProcessFacilityPendingLogs (line 189)
        // only updates Status and ModifyDate to maintain high performance.
        // It increments retry attempts when failed logs are moved back to Ready.
        Assert.Equal(DataAcquisitionLog.MaxRetryAttempts, updatedLog1.RetryAttempts);

        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog2.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_FailedWithPerTenantMaxRetries_RetriesUpToMax()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null,
            MaxRetries = 2
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 1,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123"
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 2,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123"
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log1.Id),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log2.Id),
                It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog1.Status);

        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog2.Status);
    }

    [Fact]
    public async Task ProcessPendingTailingMessages_ProducesMessagesAndUpdatesFlags()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var testTag = Guid.NewGuid().ToString("N");
        var correlationId = Guid.NewGuid().ToString();
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        var scheduledReportEntity = new ScheduledReportEntity {
            ReportTrackingId = reportTrackingId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        };

        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            TraceId = Guid.NewGuid().ToString(),
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = scheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TraceId = Guid.NewGuid().ToString(),
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = scheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ResourceAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            It.Is<Message<ResourceKey, ResourceAcquired>>(msg =>
                msg.Key != null && msg.Key.FacilityId == facilityId && msg.Key.CorrelationId == correlationId &&
                msg.Value.AcquisitionComplete == true &&
                msg.Value.ScheduledReports.Any(sr => sr.ReportTrackingId == reportTrackingId)),
            It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        Assert.True(updatedLog1.TailSent);
        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.True(updatedLog2.TailSent);
    }

    [Fact]
    public async Task ProcessPendingLogs_MultipleFacilitiesWithLargeLogCounts_ProcessesSequentiallyAndBatches()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        const int numFacilities = 4; const int logsPerFacility = 120; var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Use a large time budget so this throughput test is not sensitive to CI machine speed.
        var settings = Options.Create(new AcquisitionWorkerProcessorSettings { TimeBudgetPerRunSeconds = 300 });


        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}_{testTag}";
            facilities.Add(facilityId);

            var config = new FhirQueryConfiguration
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = "http://example.com",
                MinAcquisitionPullTime = null,
                MaxAcquisitionPullTime = null
            };
            dbContext.FhirQueryConfigurations.Add(config);

            for (int i = 1; i <= logsPerFacility; i++)
            {
                var reportTrackingId = $"Report{f}_{i}_{testTag}";

                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Pending,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = reportTrackingId,
                    PatientId = $"Patient/{i}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReportEntity = new ScheduledReportEntity {
                        ReportTrackingId = reportTrackingId,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };
                dbContext.DataAcquisitionLogs.Add(log);
            }
        }
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId.EndsWith($"_{testTag}")),
            It.IsAny<CancellationToken>()),
            Times.Exactly(numFacilities * logsPerFacility));

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        foreach (var facilityId in facilities)
        {
            var logs = assertDbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId)
                .ToList();

            Assert.Equal(logsPerFacility, logs.Count);
            Assert.All(logs, log => Assert.Equal(RequestStatus.Ready, log.Status));
        }
    }

    [Fact]
    public async Task ProcessPendingLogs_MultipleFacilitiesWithMixedPendingAndFailed_ProcessesCorrectly()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        const int numFacilities = 3;
        const int pendingPerFacility = 30; const int failedRetryablePerFacility = 20; const int failedMaxRetriesPerFacility = 10; var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}_{testTag}";
            facilities.Add(facilityId);

            var config = new FhirQueryConfiguration
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = "http://example.com",
                MinAcquisitionPullTime = null,
                MaxAcquisitionPullTime = null
            };
            dbContext.FhirQueryConfigurations.Add(config);

            for (int i = 1; i <= pendingPerFacility; i++)
            {
                var reportTrackingId = $"Pending{f}_{i}_{testTag}";
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Pending,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = reportTrackingId,
                    PatientId = $"Patient/{i}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReportEntity = new ScheduledReportEntity {
                        ReportTrackingId = reportTrackingId,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };

                dbContext.DataAcquisitionLogs.Add(log);
            }

            for (int i = 1; i <= failedRetryablePerFacility; i++)
            {
                var reportTrackingId = $"FailedRetry{f}_{i}_{testTag}";
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Failed,
                    RetryAttempts = 0,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = reportTrackingId,
                    PatientId = $"Patient/{i + pendingPerFacility}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReportEntity = new ScheduledReportEntity {
                        ReportTrackingId = reportTrackingId,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };

                dbContext.DataAcquisitionLogs.Add(log);
            }

            for (int i = 1; i <= failedMaxRetriesPerFacility; i++)
            {
                var reportTrackingId = $"MaxRetry{f}_{i}_{testTag}";
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Failed,
                    RetryAttempts = DataAcquisitionLog.MaxRetryAttempts,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = reportTrackingId,
                    PatientId = $"Patient/{i + pendingPerFacility + failedRetryablePerFacility}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReportEntity = new ScheduledReportEntity {
                        ReportTrackingId = reportTrackingId,
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };
                dbContext.DataAcquisitionLogs.Add(log);
            }
        }
        await dbContext.SaveChangesAsync();

        var processablePerFacility = pendingPerFacility + failedRetryablePerFacility;
        var totalProcessable = numFacilities * processablePerFacility;

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId.EndsWith($"_{testTag}")),
            It.IsAny<CancellationToken>()),
            Times.Exactly(totalProcessable));

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        foreach (var facilityId in facilities)
        {
            var allLogs = assertDbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId)
                .ToList();

            Assert.Equal(pendingPerFacility + failedRetryablePerFacility + failedMaxRetriesPerFacility, allLogs.Count);

            var pendingLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("Pending") && l.ReportTrackingId.EndsWith($"_{testTag}")).ToList();
            Assert.All(pendingLogs, log => Assert.Equal(RequestStatus.Ready, log.Status));

            var retryableLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("FailedRetry") && l.ReportTrackingId.EndsWith($"_{testTag}")).ToList();
            Assert.All(retryableLogs, log =>
            {
                Assert.Equal(RequestStatus.Ready, log.Status);
                Assert.Equal(1, log.RetryAttempts);
            });

            var maxRetryLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("MaxRetry") && l.ReportTrackingId.EndsWith($"_{testTag}")).ToList();
            Assert.All(maxRetryLogs, log => Assert.Equal(RequestStatus.MaxRetriesReached, log.Status));
        }
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinSameDayWindow_Dynamic_Processes()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var maxTime = new TimeSpan(23, 59, 59);

        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = TimeSpan.Zero,
            MaxAcquisitionPullTime = maxTime
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideSameDayWindow_Dynamic_Skips()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var current = DateTime.UtcNow.TimeOfDay;
        var buffer = TimeSpan.FromSeconds(30);
        var maxTime = new TimeSpan(23, 59, 59);

        TimeSpan minPull, maxPull;
        if (current + buffer <= maxTime)
        {
            minPull = current + buffer;
            maxPull = maxTime;
        }
        else
        {
            minPull = TimeSpan.Zero;
            maxPull = current - buffer;
        }

        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinMidnightSpanningWindow_Dynamic_Processes()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var current = DateTime.UtcNow.TimeOfDay;
        var noon = TimeSpan.FromHours(12);
        TimeSpan minPull, maxPull;
        if (current < noon)
        {
            minPull = TimeSpan.FromHours(23);
            maxPull = noon;
        }
        else
        {
            minPull = noon;
            maxPull = TimeSpan.FromHours(1);
        }

        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideMidnightSpanningWindow_Dynamic_Skips()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        var testTag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{testTag}";
        var reportTrackingId = $"TestReportId_{testTag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var current = DateTime.UtcNow.TimeOfDay;
        var buffer = TimeSpan.FromSeconds(30);
        var maxTime = new TimeSpan(23, 59, 59);

        TimeSpan minPull = current + buffer;
        if (minPull > maxTime) minPull = maxTime;

        TimeSpan maxPull = current - buffer;
        if (maxPull < TimeSpan.Zero) maxPull = TimeSpan.Zero;

        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReportEntity = new ScheduledReportEntity {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<ResourceKey, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer, _settings);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Value.FacilityId == facilityId),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }
}
