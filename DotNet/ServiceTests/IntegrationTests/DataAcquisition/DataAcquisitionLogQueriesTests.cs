using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DataAcquisitionLogQueriesTests : IClassFixture<DataAcquisitionIntegrationTestFixture>
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public DataAcquisitionLogQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPendingAndRetryableFailedRequests_ReturnsEligibleLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Add eligible pending log
        var pendingLog = new DataAcquisitionLog
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
        dbContext.DataAcquisitionLogs.Add(pendingLog);

        // Add eligible failed log with retries < 10
        var failedLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Failed,
            RetryAttempts = 5,
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
        dbContext.DataAcquisitionLogs.Add(failedLog);

        // Add ineligible failed log with retries >= 10
        var ineligibleLog = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.MaxRetriesReached,
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
        dbContext.DataAcquisitionLogs.Add(ineligibleLog);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var result = await queries.GetPendingAndRetryableFailedRequests();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, l => l.Id == pendingLog.Id);
        Assert.Contains(result, l => l.Id == failedLog.Id);
        Assert.DoesNotContain(result, l => l.Id == ineligibleLog.Id);
    }

    [Fact]
    public async Task GetTailingMessages_ReturnsEligibleTailingMessages()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
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

        var startDate = DateTime.UtcNow.AddDays(-1);
        var endDate = DateTime.UtcNow;

        // Add completed non-reference logs (eligible for tailing if no incomplete non-ref)
        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = startDate,
            ReportEndDate = endDate,
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
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = startDate,
            ReportEndDate = endDate,
            ScheduledReport = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        // Add an incomplete log to simulate condition where tailing is not triggered for this group
        var incompleteLog = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = startDate,
            ReportEndDate = endDate,
            ScheduledReport = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(incompleteLog);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var result = await queries.GetTailingMessages();

        // Assert
        // Since there's an incomplete non-ref log, no tailing messages should be returned for this group
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetCompleteLogAsync_ReturnsLogWithRelatedEntities()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var scheduledReport = new ScheduledReport
        {
            ReportTrackingId = "TestReportId",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        };

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = scheduledReport,
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery { FacilityId = "TestFacility", QueryType = FhirQueryType.Read, ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> { Hl7.Fhir.Model.ResourceType.Patient } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var result = await queries.GetCompleteLogAsync(log.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
        Assert.NotNull(result.ScheduledReport);
        Assert.NotEmpty(result.FhirQuery);
    }

    [Fact]
    public async Task GetLogByFacilityIdAndReportTrackingIdAndResourceType_ReturnsMatchingLog()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility";
        var reportTrackingId = "TestReportId";
        var resourceType = "Patient";

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            },
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery
                {
                    FacilityId = facilityId,
                    ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> { Hl7.Fhir.Model.ResourceType.Patient }
                }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var result = await queries.GetLogByFacilityIdAndReportTrackingIdAndResourceType(facilityId, reportTrackingId, resourceType, correlationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
    }

    [Fact]
    public async Task GetCountOfNonRefLogsIncompleteAsync_ReturnsCorrectCount()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = "TestFacility";
        var reportTrackingId = "TestReportId";

        // Add incomplete non-reference log
        var incompleteLog = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery
                { 
                    FacilityId = "TestFacility", 
                    isReference = false,
                    QueryType = FhirQueryType.Read, 
                    ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> 
                    { 
                        Hl7.Fhir.Model.ResourceType.Patient
                    }
                }
            },
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(incompleteLog);

        // Add completed reference log (should not count)
        var referenceLog = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            QueryPhase = QueryPhase.Referential,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery
                {
                    FacilityId = "TestFacility",
                    isReference = true,
                    QueryType = FhirQueryType.Read,
                    ResourceTypes = new List<Hl7.Fhir.Model.ResourceType>
                    {
                        Hl7.Fhir.Model.ResourceType.Patient
                    }
                }
            },
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(referenceLog);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var count = await queries.GetCountOfNonRefLogsIncompleteAsync(facilityId, reportTrackingId, correlationId);

        // Assert
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingResults()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";

        // Add logs
        var log1 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId1",
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId1",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = "TestReportId2",
            PatientId = "Patient/456",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = "TestReportId2",
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var searchRequest = new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            PageNumber = 1,
            PageSize = 10
        };

        // Act
        var (results, count) = await queries.SearchAsync(searchRequest);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetDataAcquisitionLogAsync_ReturnsLog()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var log = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
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

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var result = await queries.GetDataAcquisitionLogAsync(log.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
    }

    [Fact]
    public async Task GetDataAcquisitionLogStatisticsByReportAsync_ReturnsStatistics()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var reportTrackingId = "TestReportId";

        var log1 = new DataAcquisitionLog
        {
            FacilityId = "TestFacility",
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            CompletionTimeMilliseconds = 100,
            QueryPhase = QueryPhase.Initial,
            QueryType = FhirQueryType.Read,
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            },
            ResourceAcquiredIds = new List<string> { "Patient/123" },
            FhirQuery = new List<FhirQuery>
            {
                new FhirQuery { FacilityId = "TestFacility", ResourceTypes = new List<Hl7.Fhir.Model.ResourceType> { Hl7.Fhir.Model.ResourceType.Patient } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var statistics = await queries.GetDataAcquisitionLogStatisticsByReportAsync(reportTrackingId);

        // Assert
        Assert.NotNull(statistics);
        Assert.Equal(1, statistics.TotalLogs);
        Assert.True(statistics.QueryTypeCounts.ContainsKey((int)QueryType.Initial));
        Assert.True(statistics.QueryPhaseCounts.ContainsKey(QueryPhase.Initial));
        Assert.True(statistics.RequestStatusCounts.ContainsKey(RequestStatus.Completed));
        Assert.True(statistics.ResourceTypeCounts.ContainsKey("Patient"));
        Assert.True(statistics.ResourceTypeCompletionTimeMilliseconds.ContainsKey("Patient"));
    }

    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_ReturnsTrueIfSent()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";
        var reportTrackingId = "TestReportId";
        var correlationId = Guid.NewGuid().ToString();
        var referenceId = "Patient/123";

        var log = new DataAcquisitionLog
        {
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            PatientId = "Patient/123",
            ReportStartDate = DateTime.UtcNow.AddDays(-1),
            ReportEndDate = DateTime.UtcNow,
            ResourceAcquiredIds = new List<string> { referenceId },
            ScheduledReport = new ScheduledReport
            {
                ReportTrackingId = reportTrackingId,
                StartDate = DateTime.UtcNow.AddDays(-1),
                EndDate = DateTime.UtcNow
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var hasBeenSent = await queries.CheckIfReferenceResourceHasBeenSent(referenceId, reportTrackingId, facilityId, correlationId);

        // Assert
        Assert.True(hasBeenSent);
    }

    [Fact]
    public async Task GetFacilitiesWithPendingAndRetryableFailedRequests_ReturnsUniqueFacilities()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        // Add logs for different facilities
        var log1 = new DataAcquisitionLog
        {
            FacilityId = "Facility1",
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
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FacilityId = "Facility2",
            Status = RequestStatus.Failed,
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

        var log3 = new DataAcquisitionLog
        {
            FacilityId = "Facility1",
            Status = RequestStatus.Failed,
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
        dbContext.DataAcquisitionLogs.Add(log3);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var facilities = await queries.GetFacilitiesWithPendingAndRetryableFailedRequests();

        // Assert
        Assert.Equal(2, facilities.Count);
        Assert.Contains("Facility1", facilities);
        Assert.Contains("Facility2", facilities);
    }

    [Fact]
    public async Task GetNextEligibleBatchForFacility_ReturnsBatchOfLogs()
    {
        // Arrange
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        // Reset database for this test
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var facilityId = "TestFacility";

        // Add multiple eligible logs
        for (int i = 1; i <= 5; i++)
        {
            var log = new DataAcquisitionLog
            {
                FacilityId = facilityId,
                Status = RequestStatus.Pending,
                CorrelationId = Guid.NewGuid().ToString(),
                ReportTrackingId = $"TestReportId{i}",
                PatientId = "Patient/123",
                ReportStartDate = DateTime.UtcNow.AddDays(-1),
                ReportEndDate = DateTime.UtcNow,
                ScheduledReport = new ScheduledReport
                {
                    ReportTrackingId = $"TestReportId{i}",
                    StartDate = DateTime.UtcNow.AddDays(-1),
                    EndDate = DateTime.UtcNow
                }
            };
            dbContext.DataAcquisitionLogs.Add(log);
        }
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        // Act
        var batch = await queries.GetNextEligibleBatchForFacility(facilityId, null, 3);

        // Assert
        Assert.Equal(3, batch.Count);
        Assert.All(batch, l => Assert.Equal(facilityId, l.FacilityId));
    }
}