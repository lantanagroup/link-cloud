using Confluent.Kafka;
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
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Add FhirQueryConfiguration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>(); // Not used in this method, but present

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce was called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Use a new scope/DbContext for assertions to avoid change tracker cache issues
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Verify status updated to Ready
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_NoConfig_FailsLogs()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // No config added

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // No produce called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Use a new scope/DbContext for assertions to avoid change tracker cache issues
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Log status updated to Failed with note
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog.Status);
        Assert.Contains(updatedLog.Notes, note => note.Contains("missing FhirQueryConfiguration"));
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideAcquisitionWindow_SkipsProcessing()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
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

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // No produce called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Use a new scope/DbContext for assertions (even though no update, for consistency)
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Log status remains Pending
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_FailedWithRetries_RetriesUpToMax()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Add config
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add failed log with 9 retries (should retry)
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

        // Add failed log with 10 retries (should set to MaxRetriesReached)
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Produce called only for log1
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

        // Use a new scope/DbContext for assertions to avoid change tracker cache issues
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // log1: Retried, status Ready, attempts 10
        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog1.Status);
        Assert.Equal(10, updatedLog1.RetryAttempts);

        // log2: MaxRetriesReached
        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog2.Status);
    }

    [Fact]
    public async Task ProcessPendingTailingMessages_ProducesMessagesAndUpdatesFlags()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility";
        var reportTrackingId = "TestReportId";

        // Create a ScheduledReport instance
        var scheduledReport = new ScheduledReport
        {
            ReportTrackingId = reportTrackingId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        };

        // Add completed non-reference logs to trigger tailing (assuming no incomplete non-ref logs)
        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial, // non-reference
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
            TailSent = false,
            QueryPhase = QueryPhase.Initial, // non-reference
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(log2);
        await dbContext.SaveChangesAsync();

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce was called for tail message
        _fixture.ResourceAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourceAcquired.ToString(),
                It.Is<Message<string, ResourceAcquired>>(msg =>
                    msg.Key == facilityId &&
                    msg.Value.AcquisitionComplete == true &&
                    msg.Value.ScheduledReports.Any(sr => sr.ReportTrackingId == reportTrackingId)),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);

        // Use a new scope/DbContext for assertions to avoid change tracker cache issues
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // TailSent updated for the logs
        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        Assert.True(updatedLog1.TailSent);
        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.True(updatedLog2.TailSent);
    }

    [Fact]
    public async Task ProcessPendingLogs_MultipleFacilitiesWithLargeLogCounts_ProcessesInParallelAndBatches()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        const int numFacilities = 4; // Test with 4 facilities to simulate parallelism (MaxConcurrency=8)
        const int logsPerFacility = 60; // 60 logs: 3 batches (25+25+10)
        var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}";
            facilities.Add(facilityId);

            // Add FhirQueryConfiguration for each facility
            var config = new FhirQueryConfiguration
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = "http://example.com",
                MinAcquisitionPullTime = null,
                MaxAcquisitionPullTime = null
            };
            dbContext.FhirQueryConfigurations.Add(config);

            // Add 60 pending logs for each facility
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce was called exactly numFacilities * logsPerFacility times
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(numFacilities * logsPerFacility));

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Verify all logs are updated to Ready for each facility
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
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        const int numFacilities = 3;
        const int pendingPerFacility = 30; // >25 to test batching
        const int failedRetryablePerFacility = 20; // Retries < Max (10)
        const int failedMaxRetriesPerFacility = 10; // Retries >=10, should set to MaxRetriesReached
        var facilities = new List<string>();

        using var setupScope = _fixture.ServiceProvider.CreateScope();
        var dbContext = setupScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        for (int f = 1; f <= numFacilities; f++)
        {
            var facilityId = $"Facility{f}";
            facilities.Add(facilityId);

            // Add config
            var config = new FhirQueryConfiguration
            {
                FacilityId = facilityId,
                FhirServerBaseUrl = "http://example.com",
                MinAcquisitionPullTime = null,
                MaxAcquisitionPullTime = null
            };
            dbContext.FhirQueryConfigurations.Add(config);

            // Add pending logs
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

            // Add retryable failed logs (retries=5 <10)
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

            // Add max retries failed logs (retries=10)
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

        // Total processable logs per facility: pending + retryable failed
        var processablePerFacility = pendingPerFacility + failedRetryablePerFacility;
        var totalProcessable = numFacilities * processablePerFacility;

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce called exactly for processable logs
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(totalProcessable));

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        foreach (var facilityId in facilities)
        {
            var allLogs = assertDbContext.DataAcquisitionLogs
                .Where(l => l.FacilityId == facilityId)
                .ToList();

            // Total logs
            Assert.Equal(pendingPerFacility + failedRetryablePerFacility + failedMaxRetriesPerFacility, allLogs.Count);

            // Pending -> Ready
            var pendingLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("Pending")).ToList();
            Assert.All(pendingLogs, log => Assert.Equal(RequestStatus.Ready, log.Status));

            // Retryable failed -> Ready, retries incremented
            var retryableLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("FailedRetry")).ToList();
            Assert.All(retryableLogs, log =>
            {
                Assert.Equal(RequestStatus.Ready, log.Status);
                Assert.Equal(6, log.RetryAttempts); // 5 +1
            });

            // Max retries -> MaxRetriesReached, no produce
            var maxRetryLogs = allLogs.Where(l => l.ReportTrackingId.StartsWith("MaxRetry")).ToList();
            Assert.All(maxRetryLogs, log => Assert.Equal(RequestStatus.MaxRetriesReached, log.Status));
        }
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinSameDayWindow_Dynamic_Processes()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var maxTime = new TimeSpan(23, 59, 59);

        // Add FhirQueryConfiguration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = TimeSpan.Zero,
            MaxAcquisitionPullTime = maxTime
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce was called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Verify status updated to Ready
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideSameDayWindow_Dynamic_Skips()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var current = DateTime.UtcNow.TimeOfDay;
        var oneSec = TimeSpan.FromSeconds(1);
        var maxTime = new TimeSpan(23, 59, 59);

        TimeSpan minPull, maxPull;
        if (current + oneSec <= maxTime)
        {
            minPull = current + oneSec;
            maxPull = maxTime;
        }
        else
        {
            minPull = TimeSpan.Zero;
            maxPull = current - oneSec;
        }

        // Add FhirQueryConfiguration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // No produce called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Log status remains Pending
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinMidnightSpanningWindow_Dynamic_Processes()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var oneSec = TimeSpan.FromSeconds(1);

        // Add FhirQueryConfiguration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = oneSec,
            MaxAcquisitionPullTime = TimeSpan.Zero
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // Verify produce was called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<long, ReadyToAcquire>>(msg => msg.Key == log.Id && msg.Value.FacilityId == "TestFacility"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Verify status updated to Ready
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideMidnightSpanningWindow_Dynamic_Skips()
    {
        // Reset mocks to clear previous invocations
        _fixture.ReadyToAcquireProducerMock.Reset();
        _fixture.ResourceAcquiredProducerMock.Reset();

        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var current = DateTime.UtcNow.TimeOfDay;
        var oneSec = TimeSpan.FromSeconds(1);

        TimeSpan minPull, maxPull;
        if (current >= oneSec)
        {
            maxPull = current - oneSec;
            minPull = current + oneSec;
        }
        else
        {
            // Rare case, set to a known outside spanning
            maxPull = TimeSpan.Zero;
            minPull = oneSec;
        }

        // Add FhirQueryConfiguration
        var config = new FhirQueryConfiguration
        {
            FacilityId = "TestFacility",
            FhirServerBaseUrl = "http://example.com",
            MinAcquisitionPullTime = minPull,
            MaxAcquisitionPullTime = maxPull
        };
        dbContext.FhirQueryConfigurations.Add(config);

        // Add pending log
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

        // Get producers
        var readyProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<long, ReadyToAcquire>>();
        var acquiredProducer = _fixture.ServiceProvider.GetRequiredService<IProducer<string, ResourceAcquired>>();

        // Create job instance
        var loggerMock = new Mock<ILogger<AcquisitionProcessingJob>>();
        var scopeFactory = _fixture.ServiceProvider.GetRequiredService<IServiceScopeFactory>();
        var job = new AcquisitionProcessingJob(loggerMock.Object, scopeFactory, readyProducer, acquiredProducer);

        // Act
        var jobContextMock = new Mock<IJobExecutionContext>();
        jobContextMock.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        await job.Execute(jobContextMock.Object);

        // Assert
        // No produce called
        _fixture.ReadyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<long, ReadyToAcquire>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Use a new scope/DbContext for assertions
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Log status remains Pending
        var updatedLog = await assertDbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status);
    }
}