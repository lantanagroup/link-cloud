using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Bindings;
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
            MinAcquisitionPullTime = TimeSpan.FromHours(0), // Always within window for test
            MaxAcquisitionPullTime = TimeSpan.FromHours(23)
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
            MinAcquisitionPullTime = TimeSpan.FromHours(0),
            MaxAcquisitionPullTime = TimeSpan.FromHours(23)
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
}