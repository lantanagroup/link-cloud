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
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Managers;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DataAcquisitionLogManagerTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
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
        var createModel = new CreateDataAcquisitionLogModel
        {
            FacilityId = "TestFacility",
            CorrelationId = Guid.NewGuid().ToString(),
            ScheduledReport = new ScheduledReport { ReportTrackingId = "TestReport", StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
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
            ScheduledReport = null!,
            QueryType = FhirQueryType.Read,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.CreateAsync(createModel));
    }

    [Fact]
    public async Task UpdateAsync_ValidUpdate_ReturnsUpdatedModel()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = "TestReport", StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
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
        var result = await manager.UpdateAsync(updateModel);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RequestStatus.Completed, result.Status);
        Assert.NotNull(result.CompletionDate);
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
        var reportTrackingId = $"TestReport_{tag}";

        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();


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
        await manager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(logIds, facilityId, correlationId, reportTrackingId);

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
        await Assert.ThrowsAsync<NotFoundException>(() => manager.UpdateTailFlagForFacilityCorrelationIdReportTrackingId(logIds, "TestFacility", "TestCorr", "TestReport"));
    }

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
