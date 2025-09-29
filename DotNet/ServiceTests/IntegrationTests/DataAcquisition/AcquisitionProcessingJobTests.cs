using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Extensions;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Jobs;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Domain.Repositories.Implementations;
using LantanaGroup.Link.Shared.Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using System.Text;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class AcquisitionProcessingJobTests : IDisposable
{
    private readonly DataAcquisitionDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IProducer<string, ReadyToAcquire>> _readyToAcquireProducerMock;
    private readonly Mock<IProducer<string, ResourceAcquired>> _resourceAcquiredProducerMock;
    private readonly AcquisitionProcessingJob _job;
    private readonly Mock<IJobExecutionContext> _jobContextMock;
    private readonly CancellationToken _cancellationToken = CancellationToken.None;

    public AcquisitionProcessingJobTests()
    {
        var dbOptions = new DbContextOptionsBuilder<DataAcquisitionDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new DataAcquisitionDbContext(dbOptions);
        _dbContext.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);

        services.AddTransient<IEntityRepository<FhirListConfiguration>, DataEntityRepository<FhirListConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQueryConfiguration>, DataEntityRepository<FhirQueryConfiguration, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<QueryPlan>, DataEntityRepository<QueryPlan, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<ReferenceResources>, DataEntityRepository<ReferenceResources, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<FhirQuery>, DataEntityRepository<FhirQuery, DataAcquisitionDbContext>>();
        services.AddTransient<IEntityRepository<DataAcquisitionLog>, DataEntityRepository<DataAcquisitionLog, DataAcquisitionDbContext>>();

        services.AddScoped<IDatabase, Database>();

        services.AddScoped<IDataAcquisitionLogQueries, DataAcquisitionLogQueries>(sp =>
            new DataAcquisitionLogQueries(
                sp.GetRequiredService<IDatabase>(),
                sp.GetRequiredService<DataAcquisitionDbContext>(),
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataAcquisitionLogQueries>()));

        services.AddScoped<IDataAcquisitionLogManager, DataAcquisitionLogManager>(sp =>
            new DataAcquisitionLogManager(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<DataAcquisitionLogManager>(),
                sp.GetRequiredService<IDatabase>(),
                sp.GetRequiredService<IDataAcquisitionLogQueries>()));

        services.AddScoped<IFhirQueryConfigurationManager, FhirQueryConfigurationManager>(sp =>
            new FhirQueryConfigurationManager(
                sp.GetRequiredService<IDatabase>(),
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FhirQueryConfigurationManager>()));

        services.AddScoped<IFhirQueryManager, FhirQueryManager>(sp =>
            new FhirQueryManager(
                LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FhirQueryManager>(),
                sp.GetRequiredService<IDatabase>()));

        _serviceProvider = services.BuildServiceProvider();

        var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<AcquisitionProcessingJob>();

        _readyToAcquireProducerMock = new Mock<IProducer<string, ReadyToAcquire>>();
        _resourceAcquiredProducerMock = new Mock<IProducer<string, ResourceAcquired>>();

        _job = new AcquisitionProcessingJob(
            logger,
            _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            _readyToAcquireProducerMock.Object,
            _resourceAcquiredProducerMock.Object);

        _jobContextMock = new Mock<IJobExecutionContext>();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ProcessPendingLogs_NoFacilities_ShouldNotProduceMessages()
    {
        await _job.ProcessPendingLogs(_cancellationToken);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ReadyToAcquire>>(), _cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingLogs_WithPendingRequestAndNullTimes_ShouldProduceMessageAndUpdateStatus()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(m => m.Key == log.Id && m.Value.LogId == log.Id && m.Value.FacilityId == facilityId),
                _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingLogs_OutsideAcquisitionWindow_ShouldNotProduceMessage()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = new TimeSpan(0, 0, 0),
            MaxAcquisitionPullTime = new TimeSpan(1, 0, 0)
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        // Mock DateTime.UtcNow.TimeOfDay to be outside the window
        var currentTime = new TimeSpan(2, 0, 0);
        using (var timeProvider = new TimeProviderMock(currentTime))
        {
            await _job.ProcessPendingLogs(_cancellationToken);
        }

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Pending, updatedLog.Status); // Status should remain unchanged

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ReadyToAcquire>>(), _cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinAcquisitionWindow_SameDay_ShouldProduceMessage()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = new TimeSpan(0, 0, 0),
            MaxAcquisitionPullTime = new TimeSpan(23, 59, 59)
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        // Mock DateTime.UtcNow.TimeOfDay to be within the window
        var currentTime = new TimeSpan(12, 0, 0);
        using (var timeProvider = new TimeProviderMock(currentTime))
        {
            await _job.ProcessPendingLogs(_cancellationToken);
        }

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(m => m.Key == log.Id && m.Value.LogId == log.Id && m.Value.FacilityId == facilityId),
                _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingLogs_WithinAcquisitionWindow_MidnightSpanning_ShouldProduceMessage()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = new TimeSpan(20, 0, 0), // 8 PM
            MaxAcquisitionPullTime = new TimeSpan(4, 0, 0)  // 4 AM
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        // Mock DateTime.UtcNow.TimeOfDay to be within the midnight-spanning window (e.g., 9 PM)
        var currentTime = new TimeSpan(21, 0, 0);
        using (var timeProvider = new TimeProviderMock(currentTime))
        {
            await _job.ProcessPendingLogs(_cancellationToken);
        }

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(m => m.Key == log.Id && m.Value.LogId == log.Id && m.Value.FacilityId == facilityId),
                _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingLogs_MissingConfig_ShouldUpdateToFailed()
    {
        var facilityId = "test_facility";
        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog.Status);
        Assert.NotNull(updatedLog.Notes);
        Assert.Contains("Request FAILED due to missing FhirQueryConfiguration", updatedLog.Notes.FirstOrDefault() ?? string.Empty);
    }

    [Fact]
    public async Task ProcessPendingLogs_FailedRequestWithRetries_ShouldRetryAndProduceMessage()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 5,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);
        Assert.Equal(6, updatedLog.RetryAttempts);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(m => m.Key == log.Id && m.Value.LogId == log.Id && m.Value.FacilityId == facilityId),
                _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingLogs_FailedRequestMaxRetries_ShouldUpdateToMaxRetriesReached()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 10,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog.Status);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ReadyToAcquire>>(), _cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ProcessPendingLogs_ProducerThrowsException_ShouldUpdateToFailed()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        _readyToAcquireProducerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ReadyToAcquire>>(), _cancellationToken))
            .ThrowsAsync(new Exception("Producer error"));

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Failed, updatedLog.Status);
        Assert.NotNull(updatedLog.Notes);
        Assert.Contains("Failed to produce ReadyToAcquire message", updatedLog.Notes.FirstOrDefault() ?? string.Empty);
    }

    [Fact]
    public async Task ProcessPendingLogs_MultiplePages_ShouldProcessAllLogs()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var logs = Enumerable.Range(0, 50).Select(i => new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        }).ToList();
        _dbContext.DataAcquisitionLogs.AddRange(logs);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        var updatedLogs = await _dbContext.DataAcquisitionLogs
            .Where(l => l.FacilityId == facilityId)
            .ToListAsync(_cancellationToken);
        Assert.All(updatedLogs, l => Assert.Equal(RequestStatus.Ready, l.Status));

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.IsAny<Message<string, ReadyToAcquire>>(),
                _cancellationToken),
            Times.Exactly(50));
    }

    [Fact]
    public async Task ProcessPendingLogs_WithPendingRequest_VerifyCorrelationIdInHeader()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var correlationId = Guid.NewGuid().ToString();
        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = correlationId
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingLogs(_cancellationToken);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(
                    m => m.Key == log.Id &&
                         m.Value.LogId == log.Id &&
                         m.Value.FacilityId == facilityId &&
                         m.Headers.Any(h => h.Key == "X-Correlation-Id" && Encoding.UTF8.GetString(h.GetValueBytes()) == correlationId)),
                _cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ProcessPendingTailingMessages_WithTailingMessages_ShouldProduceMessageAndUpdateFlags()
    {
        var facilityId = "test_facility";
        var correlationId = Guid.NewGuid().ToString();
        var reportTrackingId = Guid.NewGuid().ToString();

        var log1 = new DataAcquisitionLog
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            TailSent = false,
            Status = RequestStatus.Completed,
            ReportStartDate = DateTime.UtcNow,
            ReportEndDate = DateTime.UtcNow,
            ResourceAcquiredIds = new List<string> { "Patient/123" },
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery
                {
                    FacilityId = facilityId,
                    isReference = false,
                    ResourceTypes = [ResourceType.Patient],
                    Id = Guid.NewGuid().ToString(),
                    DataAcquisitionLogId = Guid.NewGuid().ToString(),
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                }
            }
        };

        var log2 = new DataAcquisitionLog
        {
            Id = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            TailSent = false,
            Status = RequestStatus.Completed,
            ReportStartDate = DateTime.UtcNow,
            ReportEndDate = DateTime.UtcNow,
            ResourceAcquiredIds = new List<string> { "Patient/456" },
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery
                {
                    FacilityId = facilityId,
                    isReference = false,
                    ResourceTypes = [ResourceType.Patient],
                    Id = Guid.NewGuid().ToString(),
                    DataAcquisitionLogId = Guid.NewGuid().ToString(),
                    CreateDate = DateTime.UtcNow,
                    ModifyDate = DateTime.UtcNow
                }
            }
        };

        _dbContext.DataAcquisitionLogs.AddRange(log1, log2);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.ProcessPendingTailingMessages(_cancellationToken);

        _resourceAcquiredProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ResourceAcquired.ToString(),
                It.IsAny<Message<string, ResourceAcquired>>(),
                _cancellationToken),
            Times.Exactly(2));

        var updatedLog1 = await _dbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        var updatedLog2 = await _dbContext.DataAcquisitionLogs.FindAsync(log2.Id);

        Assert.True(updatedLog1.TailSent);
        Assert.True(updatedLog2.TailSent);
    }

    [Fact]
    public async Task ProcessPendingTailingMessages_NoTailingMessages_ShouldNotProduceMessage()
    {
        await _job.ProcessPendingTailingMessages(_cancellationToken);

        _resourceAcquiredProducerMock.Verify(
            p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ResourceAcquired>>(), _cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task Execute_WithPendingLogAndNullTimes_ShouldProcessLogsAndTailingMessages()
    {
        var facilityId = "test_facility";
        var config = new FhirQueryConfiguration
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "test_url",
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null
        };
        _dbContext.FhirQueryConfigurations.Add(config);

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString()
        };
        _dbContext.DataAcquisitionLogs.Add(log);
        await _dbContext.SaveChangesAsync(_cancellationToken);

        await _job.Execute(_jobContextMock.Object);

        var updatedLog = await _dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Equal(RequestStatus.Ready, updatedLog.Status);

        _readyToAcquireProducerMock.Verify(
            p => p.ProduceAsync(
                KafkaTopic.ReadyToAcquire.ToString(),
                It.Is<Message<string, ReadyToAcquire>>(m => m.Key == log.Id && m.Value.LogId == log.Id && m.Value.FacilityId == facilityId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Helper class to mock DateTime.UtcNow.TimeOfDay
    // Note: Using reflection to mock DateTime.UtcNow is fragile and should be replaced with a TimeProvider abstraction in production code.
    private class TimeProviderMock : IDisposable
    {
        private readonly DateTime _originalUtcNow;

        public TimeProviderMock(TimeSpan timeOfDay)
        {
            _originalUtcNow = DateTime.UtcNow;
            var mockDate = new DateTime(_originalUtcNow.Year, _originalUtcNow.Month, _originalUtcNow.Day) + timeOfDay;
            // Use reflection to set DateTime.UtcNow (note: this is not recommended for production, only for testing)
            var field = typeof(DateTime).GetField("_utcNow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, mockDate);
            }
        }

        public void Dispose()
        {
            var field = typeof(DateTime).GetField("_utcNow", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(null, _originalUtcNow);
            }
        }
    }
}