using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AcquisitionProcessingJobTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public AcquisitionProcessingJobTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ProcessPendingLogs_WithValidConfigAndWithinWindow_ProducesMessagesAndUpdatesStatus()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var logManager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var createLog = new CreateDataAcquisitionLogModel
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            PatientId = "Patient/123",
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };

        var log = await logManager.CreateAsync(createLog);

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_NoConfig_FailsLogs()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();


        var log = new DataAcquisitionLog
        {
            FacilityId = "MissingConfigFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog.Status);
        Assert.Contains(updatedLog.Notes, note => note.Contains("missing FhirQueryConfiguration"));
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideAcquisitionWindow_SkipsProcessing()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = DateTime.UtcNow.AddHours(2).TimeOfDay,
            MaxAcquisitionPullTime = DateTime.UtcNow.AddHours(3).TimeOfDay
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
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

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log1 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Failed,
            RetryAttempts = 9,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Failed,
            RetryAttempts = 10,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

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
        Assert.Equal(10, updatedLog1.RetryAttempts);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility";
        var reportTrackingId = "TestReportId";

        var scheduledReport = new ScheduledReport
        {
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
            ScheduledReport = scheduledReport
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
            ScheduledReport = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ResourceAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ResourceAcquired.ToString(),
            It.Is<Message<string, ResourceAcquired>>(msg =>
                msg.Key == facilityId &&
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
    public async Task ProcessPendingLogs_MultipleFacilitiesWithLargeLogCounts_ProcessesInParallelAndBatches()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        const int numFacilities = 4; const int logsPerFacility = 60; var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}";
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
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Pending,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = $"Report{i}",
                    PatientId = $"Patient/{i}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReport = new ScheduledReport
                    {
                        ReportTrackingId = $"Report{i}",
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };
                dbContext.DataAcquisitionLogs.Add(log);
            }
        }
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
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

        const int numFacilities = 3;
        const int pendingPerFacility = 30; const int failedRetryablePerFacility = 20; const int failedMaxRetriesPerFacility = 10; var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}";
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
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Pending,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = $"Pending{i}",
                    PatientId = $"Patient/{i}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReport = new ScheduledReport
                    {
                        ReportTrackingId = $"Pending{i}",
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };
                dbContext.DataAcquisitionLogs.Add(log);
            }

            for (int i = 1; i <= failedRetryablePerFacility; i++)
            {
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Failed,
                    RetryAttempts = 5,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = $"FailedRetry{i}",
                    PatientId = $"Patient/{i + pendingPerFacility}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReport = new ScheduledReport
                    {
                        ReportTrackingId = $"FailedRetry{i}",
                        StartDate = DateTime.UtcNow.AddDays(-1),
                        EndDate = DateTime.UtcNow
                    }
                };
                dbContext.DataAcquisitionLogs.Add(log);
            }

            for (int i = 1; i <= failedMaxRetriesPerFacility; i++)
            {
                var log = new DataAcquisitionLog
                {
                    FacilityId = facilityId,
                    Status = RequestStatus.Failed,
                    RetryAttempts = 10,
                    CorrelationId = Guid.NewGuid().ToString(),
                    ReportTrackingId = $"MaxRetry{i}",
                    PatientId = $"Patient/{i + pendingPerFacility + failedRetryablePerFacility}",
                    ReportStartDate = DateTime.UtcNow.AddDays(-1),
                    ReportEndDate = DateTime.UtcNow,
                    ScheduledReport = new ScheduledReport
                    {
                        ReportTrackingId = $"MaxRetry{i}",
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
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
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

            var pendingLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("Pending")).ToList();
            Assert.All(pendingLogs, log => Assert.Equal(RequestStatus.Ready, log.Status));

            var retryableLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("FailedRetry")).ToList();
            Assert.All(retryableLogs, log =>
            {
                Assert.Equal(RequestStatus.Ready, log.Status);
                Assert.Equal(6, log.RetryAttempts);
            });

            var maxRetryLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("MaxRetry")).ToList();
            Assert.All(maxRetryLogs, log => Assert.Equal(RequestStatus.MaxRetriesReached, log.Status));
        }
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinSameDayWindow_Dynamic_Processes()
    {
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var maxTime = new TimeSpan(23, 59, 59);

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = TimeSpan.Zero,
            MaxAcquisitionPullTime = maxTime
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
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

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

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
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
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

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

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
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            KafkaTopic.ReadyToAcquire.ToString(),
            It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
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

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var current = DateTime.UtcNow.TimeOfDay;
        var buffer = TimeSpan.FromSeconds(30);
        var maxTime = new TimeSpan(23, 59, 59);

        TimeSpan minPull = current + buffer;
        if (minPull > maxTime) minPull = maxTime;

        TimeSpan maxPull = current - buffer;
        if (maxPull < TimeSpan.Zero) maxPull = TimeSpan.Zero;

        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<long, ReadyToAcquire>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }
}