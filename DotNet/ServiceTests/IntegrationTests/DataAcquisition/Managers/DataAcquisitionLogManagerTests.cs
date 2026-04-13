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
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
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
        return new DataAcquisitionLogManager(logger, database, dbContext, queries);
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
            Frequency = Frequency.Adhoc,
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
    public async Task CreateAsync_InvalidModel_ThrowsArgumentNull()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var createModel = new CreateDataAcquisitionLogModel()
        {
            FacilityId = null!,
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(createModel));
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
    public async Task UpdateAsync_NoExistingLog_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);
        var updateModel = new UpdateDataAcquisitionLogModel
        {
            Id = 999,
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
    public async Task DeleteAsync_NoExistingId_ThrowsNotFound()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var manager = CreateManager(scope);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => manager.DeleteAsync(999));
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
            Frequency = Frequency.Adhoc,
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
        var logIds = new List<long> { 999 };

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log1 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CreateDate = DateTime.UtcNow.AddHours(-48)
        };
        var log2 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.MaxRetriesReached, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Skipped, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Cancelled, CreateDate = DateTime.UtcNow.AddHours(-48) }
        );
        await dbContext.SaveChangesAsync();

        var ids = dbContext.DataAcquisitionLogs.Select(l => l.Id).ToList();
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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        // Act
        var result = await manager.CancelBulkAsync(new List<long> { 9999, 8888 }, 24);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CancelBulkAsync_SetsModifyDate()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var eligible = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var completed = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var recent = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-1) };
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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var targetLog = new DataAcquisitionLog { FacilityId = "FacilityA", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var otherLog = new DataAcquisitionLog { FacilityId = "FacilityB", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(targetLog, otherLog);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);
        var filter = new SearchDataAcquisitionLogRequest { FacilityId = "FacilityA" };

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var match = new DataAcquisitionLog { FacilityId = "F1", PatientId = "P123", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var noMatch = new DataAcquisitionLog { FacilityId = "F1", PatientId = "P456", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(match, noMatch);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { PatientId = "P123" }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var matchReportTrackingId = Guid.NewGuid();
        var noMatchReportTrackingId = Guid.NewGuid();
        dbContext.ScheduledReports.AddRange(
            new ScheduledReportEntity
            {
                ReportTrackingId = matchReportTrackingId,
                Frequency = Frequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            },
            new ScheduledReportEntity
            {
                ReportTrackingId = noMatchReportTrackingId,
                Frequency = Frequency.Adhoc,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            });
        var match = new DataAcquisitionLog { FacilityId = "F1", ReportTrackingId = matchReportTrackingId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var noMatch = new DataAcquisitionLog { FacilityId = "F1", ReportTrackingId = noMatchReportTrackingId, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var initial = new DataAcquisitionLog { FacilityId = "F1", QueryPhase = QueryPhase.Initial, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var supplemental = new DataAcquisitionLog { FacilityId = "F1", QueryPhase = QueryPhase.Supplemental, Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(initial, supplemental);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { QueryPhase = QueryPhase.Initial }, 24);

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
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "NonExistent" }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Cancelled, CreateDate = DateTime.UtcNow.AddHours(-48) }
        );
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1" }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.DataAcquisitionLogs.AddRange(
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Failed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Completed, CreateDate = DateTime.UtcNow.AddHours(-48) },
            new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-1) }
        );
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1" }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var deleted = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = true };
        var active = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = false };
        dbContext.DataAcquisitionLogs.AddRange(deleted, active);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1", IncludeDeleted = false }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var deleted = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = true };
        var active = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48), IsDeleted = false };
        dbContext.DataAcquisitionLogs.AddRange(deleted, active);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1", IncludeDeleted = true }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var cutoff = DateTime.UtcNow.AddDays(-3);
        var oldLog = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddDays(-5) };
        var newLog = new DataAcquisitionLog { FacilityId = "F1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddDays(-1) };
        dbContext.DataAcquisitionLogs.AddRange(oldLog, newLog);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1", CreatedBefore = cutoff }, 0);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "F1",
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
            new SearchDataAcquisitionLogRequest { FacilityId = "F1" }, 24);

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

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Only the log matching both FacilityId AND PatientId should be included
        var match = new DataAcquisitionLog { FacilityId = "F1", PatientId = "P1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var wrongPatient = new DataAcquisitionLog { FacilityId = "F1", PatientId = "P2", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        var wrongFacility = new DataAcquisitionLog { FacilityId = "F2", PatientId = "P1", Status = RequestStatus.Pending, CreateDate = DateTime.UtcNow.AddHours(-48) };
        dbContext.DataAcquisitionLogs.AddRange(match, wrongPatient, wrongFacility);
        await dbContext.SaveChangesAsync();

        var manager = CreateManager(scope);

        // Act
        var (requested, cancelled) = await manager.CancelByFilterAsync(
            new SearchDataAcquisitionLogRequest { FacilityId = "F1", PatientId = "P1" }, 24);

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

