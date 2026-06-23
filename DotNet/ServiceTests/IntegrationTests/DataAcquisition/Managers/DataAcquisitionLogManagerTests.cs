using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using Medallion.Threading;
using Microsoft.EntityFrameworkCore;
using LantanaGroup.Link.Shared.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using ScheduledFrequency = LantanaGroup.Link.Shared.Application.Models.Frequency;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DataAcquisitionLogManagerTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public DataAcquisitionLogManagerTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private IDataAcquisitionLogManager CreateManager(IServiceScope scope)
    {
        var logger = new Mock<ILogger<DataAcquisitionLogManager>>().Object;
        var database = scope.ServiceProvider.GetRequiredService<IDatabase>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var semaphoreProvider = CreateSemaphoreProviderMock();
        var resourceCache = scope.ServiceProvider.GetRequiredService<IResourceCache>();
        return new DataAcquisitionLogManager(logger, database, dbContext, queries, semaphoreProvider, resourceCache);
    }

    private static IDistributedSemaphoreProvider CreateSemaphoreProviderMock()
    {
        var handle = new Mock<IDistributedSynchronizationHandle>();
        var semaphore = new Mock<IDistributedSemaphore>();
        var provider = new Mock<IDistributedSemaphoreProvider>();

        semaphore
            .Setup(s => s.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IDistributedSynchronizationHandle?>(handle.Object));
        provider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(semaphore.Object);

        return provider.Object;
    }

    [Fact]
    public async Task CreateAsync_ValidModel_ReturnsLogModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var manager = CreateManager(scope);
        var queries = _fixture.ServiceProvider.CreateScope().ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();
        var reportTrackingId = Guid.NewGuid();
        dbContext.ScheduledReports.Add(new ScheduledReportEntity
        {
            ReportTrackingId = reportTrackingId,
            Frequency = ScheduledFrequency.Adhoc,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var createModel = new CreateDataAcquisitionLogModel
        {
            FacilityId = "TestFacility",
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId.ToString(),
            QueryPhase = QueryPhase.Initial,
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            Priority = AcquisitionPriority.Normal,
            FhirQuery = new List<CreateFhirQueryModel>()
            {
                new CreateFhirQueryModel
                {
                    FacilityId = "TestFacility",
                    IsReference = false,
                    Paged = 25,
                    QueryType = FhirQueryType.Read,
                    QueryParameters = new List<string>() { "Test "},
                    ResourceTypes = new List<ResourceType>() { ResourceType.Patient },
                    MeasureId = "TestMeasureId",
                    ResourceReferenceTypes = new List<CreateResourceReferenceTypeModel>()
                    {
                        new CreateResourceReferenceTypeModel
                        {
                            FacilityId = "TestFacility",
                            QueryPhase = QueryPhase.Initial,
                            ResourceType = "Patient",
                            CreateDate = DateTime.UtcNow,
                            ModifyDate = DateTime.UtcNow,
                        }
                    }
                }
            }
        };

        // Act
        var result = await manager.CreateAsync(createModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TestFacility", result.FacilityId);
        Assert.Equal(RequestStatus.Pending, result.Status);
        Assert.Equal(result.Id, result.FhirQuery.First().DataAcquisitionLogId);

        var log = await queries.GetAsync(result.Id);

        Assert.NotNull(log);
        Assert.Equal(result.Id, log.Id);
        Assert.Equal(result.Id, log.Id);
        Assert.NotEmpty(log.FhirQuery);
    }

    [Fact]
    public async Task UpdateAsync_ValidUpdate_PersistsUpdatedFields()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = log.Id,
            Status = RequestStatus.Completed,
            CompletionDate = DateTime.UtcNow,
            CompletionTimeMilliseconds = 1000
        };

        // Act
        await manager.UpdateAsync(updateModel);

        // Assert
        var updated = await dbContext.DataAcquisitionLogs.AsNoTracking().FirstAsync(x => x.Id == log.Id);
        Assert.Equal(RequestStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletionDate);
    }

    [Fact]
    public async Task UpdateStatusBatchAsync_ToCompleted_SetsCompletionDate()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var log = new DataAcquisitionLog
        {
            FacilityId = $"TestFacility_{Guid.NewGuid():N}",
            Status = RequestStatus.Processing,
            CompletionDate = null
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var beforeUpdate = DateTime.UtcNow.AddSeconds(-1);
        var manager = CreateManager(scope);

        var updated = await manager.UpdateStatusBatchAsync([log.Id], RequestStatus.Completed, incrementRetry: false);

        Assert.Equal(1, updated);

        await dbContext.Entry(log).ReloadAsync();
        Assert.Equal(RequestStatus.Completed, log.Status);
        Assert.NotNull(log.CompletionDate);
        Assert.True(log.CompletionDate >= beforeUpdate, $"CompletionDate {log.CompletionDate} should be >= {beforeUpdate}");
    }

    [Fact]
    public async Task UpdateAsync_NoExistingLog_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        // Negative id is guaranteed absent: SQL Server identity values are always positive,
        // so this stays "not found" regardless of how many logs prior tests left in the shared DB.
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = -1,
            Status = RequestStatus.Completed
        };

        // Act & Assert
        await Assert.ThrowsAsync<DataAcquisitionLogNotFoundException>(() => manager.UpdateAsync(updateModel));
    }

    [Fact]
    public async Task DeleteAsync_ValidId_DeletesLog()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        await manager.DeleteAsync(log.Id);

        // Assert
        var deletedLog = await dbContext.DataAcquisitionLogs.FindAsync(log.Id);
        Assert.Null(deletedLog);
    }

    [Fact]
    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId_ValidIds_UpdatesFlags()
    {
        // Arrange
        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{tag}";
        var correlationId = $"TestCorr_{tag}";
        var reportTrackingId = Guid.NewGuid();

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        dbContext.ScheduledReports.Add(new ScheduledReportEntity
        {
            ReportTrackingId = reportTrackingId,
            Frequency = ScheduledFrequency.Adhoc,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        });


        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            TailSent = false
        };
        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            TailSent = false
        };
        dbContext.DataAcquisitionLogs.AddRange(log1, log2);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var logIds = new List<long> { log1.Id, log2.Id };

        // Act
        await manager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(logIds, facilityId, correlationId, reportTrackingId.ToString());

        // Assert
        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var updatedLog1 = await assertDbContext.DataAcquisitionLogs.FindAsync(log1.Id);
        var updatedLog2 = await assertDbContext.DataAcquisitionLogs.FindAsync(log2.Id);

        Assert.NotNull(updatedLog1);
        Assert.NotNull(updatedLog2);
        Assert.True(updatedLog1.TailSent);
        Assert.True(updatedLog2.TailSent);
    }

    [Fact]
    public async Task UpdateTailFlagForFacilityCorrelationIdReportTrackingId_NoLog_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        // Negative id is guaranteed absent: SQL Server identity values are always positive,
        // so this stays "not found" regardless of how many logs prior tests left in the shared DB.
        var logIds = new List<long> { -1 };

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(logIds, "TestFacility", "TestCorr", Guid.NewGuid().ToString()));
    }

    // ==================== CancelBulkAsync Tests ====================

    [Fact]
    public async Task CancelBulkAsync_EligibleLogs_CancelsAndReturnsCount()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"TestFacility_{Guid.NewGuid():N}";

        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        dbContext.DataAcquisitionLogs.AddRange(log1, log2);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long> { log1.Id, log2.Id }, 24);

        // Assert
        Assert.Equal(2, result);

        await dbContext.Entry(log1).ReloadAsync();
        await dbContext.Entry(log2).ReloadAsync();
        Assert.Equal(RequestStatus.Cancelled, log1.Status);
        Assert.Equal(RequestStatus.Cancelled, log2.Status);
    }

    [Fact]
    public async Task CancelBulkAsync_TerminalStatuses_NotCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        var seeded = new[]
        {
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.MaxRetriesReached, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Skipped, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Cancelled, CreateDate = DateTime.UtcNow.AddHours(-48) }
        };
        dbContext.DataAcquisitionLogs.AddRange(seeded);
        await dbContext.SaveChangesAsync();

        var ids = seeded.Select(l => l.Id).ToList();
        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(ids, 24);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CancelBulkAsync_LogTooRecent_NotCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var log = new DataAcquisitionLog
        {
            FacilityId = $"TestFacility_{Guid.NewGuid():N}",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-1)
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long> { log.Id }, 24);

        // Assert
        Assert.Equal(0, result);
        await dbContext.Entry(log).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, log.Status);
    }

    [Fact]
    public async Task CancelBulkAsync_MinAgeZero_CancelsRecentLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var log = new DataAcquisitionLog
        {
            FacilityId = $"TestFacility_{Guid.NewGuid():N}",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long> { log.Id }, 0);

        // Assert
        Assert.Equal(1, result);
        await dbContext.Entry(log).ReloadAsync();
        Assert.Equal(RequestStatus.Cancelled, log.Status);
    }

    [Fact]
    public async Task CancelBulkAsync_NonExistingIds_ReturnsZero()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long> { -9999, -8888 }, 24);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CancelBulkAsync_SetsModifyDate()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var log = new DataAcquisitionLog
        {
            FacilityId = $"TestFacility_{Guid.NewGuid():N}",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48),
            ModifyDate = null
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var beforeCancel = DateTime.UtcNow.AddSeconds(-1);
        var manager = CreateManager(scope);

        // Act
        await manager.CancelBulkAsync(new List<long> { log.Id }, 24);

        // Assert
        await dbContext.Entry(log).ReloadAsync();
        Assert.NotNull(log.ModifyDate);
        Assert.True(log.ModifyDate >= beforeCancel, $"ModifyDate {log.ModifyDate} should be >= {beforeCancel}");
    }

    [Fact]
    public async Task CancelBulkAsync_MixedEligibility_OnlyCancelsEligible()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        var eligible = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var completed = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var recent = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-1) };
        dbContext.DataAcquisitionLogs.AddRange(eligible, completed, recent);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(
            new List<long> { eligible.Id, completed.Id, recent.Id }, 24);

        // Assert
        Assert.Equal(1, result);
        await dbContext.Entry(eligible).ReloadAsync();
        await dbContext.Entry(completed).ReloadAsync();
        await dbContext.Entry(recent).ReloadAsync();
        Assert.Equal(RequestStatus.Cancelled, eligible.Status);
        Assert.Equal(RequestStatus.Completed, completed.Status);
        Assert.Equal(RequestStatus.Pending, recent.Status);
    }

    [Fact]
    public async Task CancelBulkAsync_EmptyIds_ReturnsZero()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long>(), 24);

        // Assert
        Assert.Equal(0, result);
    }

    // ==================== CancelByFilterAsync Tests ====================

    [Fact]
    public async Task CancelByFilterAsync_ByFacilityId_CancelsOnlyMatchingFacility()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var targetFacilityId = $"FacilityA_{tag}";
        var otherFacilityId = $"FacilityB_{tag}";

        var targetLog = new DataAcquisitionLog { FacilityId = targetFacilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var otherLog = new DataAcquisitionLog { FacilityId = otherFacilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(targetLog, otherLog);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var filter = new SearchDataAcquisitionLogRequest { FacilityId = targetFacilityId };

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(filter, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
        await dbContext.Entry(otherLog).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, otherLog.Status);
    }

    [Fact]
    public async Task CancelByFilterAsync_ByPatientId_CancelsMatchingLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"F1_{tag}";
        var matchPatientId = $"P123_{tag}";
        var noMatchPatientId = $"P456_{tag}";

        var match = new DataAcquisitionLog { FacilityId = facilityId, PatientId = matchPatientId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var noMatch = new DataAcquisitionLog { FacilityId = facilityId, PatientId = noMatchPatientId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(match, noMatch);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId, PatientId = matchPatientId }, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
        await dbContext.Entry(noMatch).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, noMatch.Status);
    }

    [Fact]
    public async Task CancelByFilterAsync_ByReportTrackingId_CancelsMatchingLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";
        var matchReportTrackingId = Guid.NewGuid();
        var noMatchReportTrackingId = Guid.NewGuid();
        dbContext.ScheduledReports.AddRange(
            new ScheduledReportEntity
            {
                ReportTrackingId = matchReportTrackingId,
                Frequency = ScheduledFrequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            },
            new ScheduledReportEntity
            {
                ReportTrackingId = noMatchReportTrackingId,
                Frequency = ScheduledFrequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            });
        var match = new DataAcquisitionLog { FacilityId = facilityId, ReportTrackingId = matchReportTrackingId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var noMatch = new DataAcquisitionLog { FacilityId = facilityId, ReportTrackingId = noMatchReportTrackingId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(match, noMatch);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { ReportTrackingId = matchReportTrackingId.ToString() }, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_ByQueryPhase_CancelsMatchingLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";
        var initial = new DataAcquisitionLog { FacilityId = facilityId, QueryPhase = QueryPhase.Initial, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var supplemental = new DataAcquisitionLog { FacilityId = facilityId, QueryPhase = QueryPhase.Supplemental, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(initial, supplemental);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId, QueryPhase = QueryPhase.Initial }, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
        await dbContext.Entry(supplemental).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, supplemental.Status);
    }

    [Fact]
    public async Task CancelByFilterAsync_NoMatchingLogs_ReturnsZeros()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = $"NonExistent_{Guid.NewGuid():N}" }, 24);

        // Assert
        Assert.Equal(0, requested);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_AllTerminal_RequestedButNoneCancelled()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Cancelled, CreateDate = DateTime.UtcNow.AddHours(-48) }
        );
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId }, 24);

        // Assert
        Assert.Equal(2, requested);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_MixedEligibility_ReturnsCorrectCounts()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Failed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-1) }
        );
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId }, 24);

        // Assert
        Assert.Equal(4, requested);
        Assert.Equal(2, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_ExcludesDeletedByDefault()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        var deleted = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = true };
        var active = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = false };
        dbContext.DataAcquisitionLogs.AddRange(deleted, active);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId, IncludeDeleted = false }, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_IncludesDeletedWhenRequested()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";

        var deleted = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = true };
        var active = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = false };
        dbContext.DataAcquisitionLogs.AddRange(deleted, active);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId, IncludeDeleted = true }, 24);

        // Assert
        Assert.Equal(2, requested);
        Assert.Equal(2, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_CreatedBeforeFilter_OnlyCancelsOlderLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var oldLog = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddDays(-5) };
        var newLog = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddDays(-1) };
        dbContext.DataAcquisitionLogs.AddRange(oldLog, newLog);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId, CreatedBefore = cutoff }, 0);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public async Task CancelByFilterAsync_SetsModifyDate()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var facilityId = $"F1_{Guid.NewGuid():N}";
        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48),
            ModifyDate = null
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var beforeCancel = DateTime.UtcNow.AddSeconds(-1);
        var manager = CreateManager(scope);

        // Act
        await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facilityId }, 24);

        // Assert
        await dbContext.Entry(log).ReloadAsync();
        Assert.Equal(RequestStatus.Cancelled, log.Status);
        Assert.NotNull(log.ModifyDate);
        Assert.True(log.ModifyDate >= beforeCancel, $"ModifyDate {log.ModifyDate} should be >= {beforeCancel}");
    }

    [Fact]
    public async Task CancelByFilterAsync_MultipleFilters_AppliesAll()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facility1 = $"F1_{tag}";
        var facility2 = $"F2_{tag}";
        var patient1 = $"P1_{tag}";
        var patient2 = $"P2_{tag}";

        // Only the log matching both FacilityId AND PatientId should be included
        var match = new DataAcquisitionLog { FacilityId = facility1, PatientId = patient1, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var wrongPatient = new DataAcquisitionLog { FacilityId = facility1, PatientId = patient2, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var wrongFacility = new DataAcquisitionLog { FacilityId = facility2, PatientId = patient1, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(match, wrongPatient, wrongFacility);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = facility1, PatientId = patient1 }, 24);

        // Assert
        Assert.Equal(1, requested);
        Assert.Equal(1, cancelled);
    }

    // ==================== GetPendingRequests Tests ====================

    [Fact]
    public async Task GetPendingRequests_ReturnsPendingLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();


        var pendingLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            ExecutionDate = DateTime.UtcNow.AddDays(-1),
            Priority = AcquisitionPriority.Normal
        };
        var completedLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
            ExecutionDate = DateTime.UtcNow.AddDays(-1)
        };
        dbContext.DataAcquisitionLogs.AddRange(pendingLog, completedLog);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            RequestStatuses = [RequestStatus.Pending, RequestStatus.Failed]
        });

        // Assert
        Assert.True(result.Records.Any());
        foreach (var rec in result.Records)
        {
            if (rec.Status != RequestStatus.Pending && rec.Status != RequestStatus.Failed)
                Assert.Fail("Search results should only have Pending and Failed statuses");
        }
    }

    [Fact]
    public async Task SetMaxRetriesReachedWithNoteBatchAsync_SetsStatusAndWritesNote()
    {
        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"TestFacility_{tag}";
        const string expectedNote = "FhirQueryConfiguration not found for this facility.";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var log1 = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Pending };
        var log2 = new DataAcquisitionLog { FacilityId = facilityId, Status = RequestStatus.Failed, RetryAttempts = 2 };
        dbContext.DataAcquisitionLogs.AddRange(log1, log2);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        var updated = await manager.SetMaxRetriesReachedWithNoteBatchAsync(
            new[] { log1.Id, log2.Id },
            expectedNote);

        Assert.Equal(2, updated);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var updatedLog1 = await assertDb.DataAcquisitionLogs.FindAsync(log1.Id);
        var updatedLog2 = await assertDb.DataAcquisitionLogs.FindAsync(log2.Id);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog1.Status);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedLog2.Status);

        var notes = await assertDb.DataAcquisitionLogNotes
            .Where(n => n.DataAcquisitionLogId == log1.Id || n.DataAcquisitionLogId == log2.Id)
            .ToListAsync();
        Assert.Equal(2, notes.Count);
        Assert.All(notes, n => Assert.Equal(expectedNote, n.Note));
    }

    [Fact]
    public async Task SetMaxRetriesReachedWithNoteBatchAsync_NoMatchingIds_ReturnsZeroAndWritesNoNotes()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        var updated = await manager.SetMaxRetriesReachedWithNoteBatchAsync(
            new[] { -1L, -2L },
            "should not be written");

        Assert.Equal(0, updated);

        using var assertScope = _fixture.ServiceProvider.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var orphanNotes = await assertDb.DataAcquisitionLogNotes
            .Where(n => n.DataAcquisitionLogId == -1 || n.DataAcquisitionLogId == -2)
            .ToListAsync();
        Assert.Empty(orphanNotes);
    }
}

