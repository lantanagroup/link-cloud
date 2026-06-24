using IntegrationTests.DataAcquisition;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Context;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition.Queries;

[Collection("IntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class DataAcquisitionLogQueriesTests
{
    private readonly DataAcquisitionIntegrationTestFixture _fixture;

    public DataAcquisitionLogQueriesTests(DataAcquisitionIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetPendingAndRetryableFailedRequests_ReturnsEligibleLogs()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"Eligible_{tag}";
        var reportTrackingId = Guid.NewGuid();
        var scheduledReport = new ScheduledReportEntity
        {
            ReportTrackingId = reportTrackingId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        };

        var pendingLog = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(pendingLog);

        var failedLog = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Failed,
            RetryAttempts = 5,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(failedLog);

        var ineligibleLog = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.MaxRetriesReached,
            RetryAttempts = 10,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(ineligibleLog);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            RequestStatuses = [RequestStatus.Pending, RequestStatus.Failed]
        });

        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Records, l => l.Id == pendingLog.Id);
        Assert.Contains(result.Records, l => l.Id == failedLog.Id);
        Assert.DoesNotContain(result.Records, l => l.Id == ineligibleLog.Id);
    }

    [Fact]
    public async Task GetTailingMessages_ReturnsEligibleTailingMessages()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = $"Tail_{Guid.NewGuid():N}";
        var reportTrackingId = Guid.NewGuid();

        var ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        // Incomplete log prevents tailing for this group
        var incompleteLog = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(incompleteLog);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetTailingMessages();

        // Our group has an incomplete log, so it should NOT appear in tailing messages
        Assert.DoesNotContain(result, m => m.CorrelationId == correlationId);
    }

    [Fact]
    public async Task GetTailingMessages_WithMixedStatusesIncludingCompletedOpOutcome_ReturnsTailingMessage()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = $"TailMixed_{Guid.NewGuid():N}";
        var reportTrackingId = Guid.NewGuid();

        var ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetTailingMessages();

        var tailingMessage = Assert.Single(result, m => m.CorrelationId == correlationId);
        Assert.Equal(facilityId, tailingMessage.FacilityId);
        Assert.Contains(log1.Id, tailingMessage.LogIds);
        Assert.Contains(log2.Id, tailingMessage.LogIds);
    }

    [Fact]
    public async Task GetTailingMessages_WithCompleted_ReturnsEligibleTailingMessages()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = $"TailCompleted_{Guid.NewGuid():N}";
        var reportTrackingId = Guid.NewGuid();

        var ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = ScheduledReportEntity
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetTailingMessages();

        var message = Assert.Single(result, m => m.CorrelationId == correlationId);
        Assert.Equal(facilityId, message.FacilityId);
        Assert.Contains(log1.Id, message.LogIds);
    }

    [Fact]
    public async Task GetCompleteLogAsync_ReturnsLogWithRelatedEntities()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"CompleteLog_{tag}";

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Read, FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetAsync(log.Id);

        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
        Assert.NotNull(result.ScheduledReport);
        Assert.NotEmpty(result.FhirQuery);
    }

    [Fact]
    public async Task DataAcquisitionLog_Search_ByStatus_ReturnsMatchingLog()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var correlationId = Guid.NewGuid().ToString();
        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"SearchStatus_{tag}";
        var reportTrackingId = Guid.NewGuid();

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var refResource = new ReferenceResources { FacilityId = "test", QueryPhase = QueryPhase.Initial, ReferenceResource = "TestResource", ResourceId = "patient/12354", ResourceType = ResourceType.Encounter.ToString() };
        dbContext.ReferenceResources.Add(refResource);
        await dbContext.SaveChangesAsync();
        log.ReferenceResources.Add(refResource);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = (await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            ReportTrackingId = reportTrackingId.ToString(),
            CorrelationId = correlationId
        })).Records.FirstOrDefault();

        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
    }

    [Fact]
    public async Task SearchAsync_CompletedLogWithoutCompletionDate_ReturnsModifyDateAsCompletionDate()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"CompletedDateFallback_{tag}";
        var reportTrackingId = Guid.NewGuid();
        var modifyDate = DateTime.UtcNow.AddMinutes(-5);

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CompletionDate = null,
            ModifyDate = modifyDate,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            RequestStatuses = [RequestStatus.Completed],
            PageNumber = 1,
            PageSize = 10
        });

        var record = Assert.Single(result.Records);
        Assert.Equal(log.Id, record.Id);
        Assert.Equal(modifyDate, record.CompletionDate);
    }

    [Fact]
    public async Task SearchQueryLogSummaryAsync_CompletedLogWithoutCompletionDate_ReturnsModifyDateAsCompletionDate()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"CompletedDateListFallback_{tag}";
        var reportTrackingId = Guid.NewGuid();
        var modifyDate = DateTime.UtcNow.AddMinutes(-5);

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CompletionDate = null,
            ModifyDate = modifyDate,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            QueryType = FhirQueryType.Read,
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    FacilityId = facilityId,
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient }
                    }
                }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.SearchQueryLogSummaryAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            RequestStatuses = [RequestStatus.Completed],
            PageNumber = 1,
            PageSize = 10
        });

        var record = Assert.Single(result.Records);
        Assert.Equal(log.Id, record.Id);
        Assert.Equal(modifyDate, record.CompletionDate);
    }

    [Fact]
    public async Task SearchAsync_ReturnsMatchingResults()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"SearchMatch_{tag}";

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/456",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var results = await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            PageNumber = 1,
            PageSize = 10
        });

        Assert.Equal(2, results.Records.Count);
    }

    [Fact]
    public async Task GetDataAcquisitionLogAsync_ReturnsLog()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"GetLog_{tag}";

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>()
            {
                new FhirQuery
                {
                    FacilityId = facilityId, IsReference = false, Paged = 25,
                    QueryType = FhirQueryType.Read, QueryParameters = new List<string>() { "Test " },
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } },
                    MeasureId = "TestMeasureId",
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                    {
                        new ResourceReferenceType { FacilityId = facilityId, QueryPhase = QueryPhase.Initial, ResourceType = "Patient", CreateDate = DateTime.UtcNow, ModifyDate = DateTime.UtcNow }
                    }
                }
            },
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var refGetLog = new ReferenceResources { FacilityId = "test", QueryPhase = QueryPhase.Polling, ReferenceResource = "Test", ResourceId = Guid.NewGuid().ToString(), ResourceType = "test", CreateDate = DateTime.UtcNow };
        dbContext.ReferenceResources.Add(refGetLog);
        await dbContext.SaveChangesAsync();
        log.ReferenceResources.Add(refGetLog);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetAsync(log.Id);

        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
        Assert.NotEmpty(result.FhirQuery);
        Assert.NotEmpty(result.FhirQuery.First().ResourceReferenceTypes);
        Assert.True(result.ReferenceResourceCount > 0);
    }

    [Fact]
    public async Task SearchAsync_By_ResourceType_ReturnsLog()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"SearchRT_{tag}";
        var correlationId = Guid.NewGuid().ToString();

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = correlationId,
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>()
            {
                new FhirQuery
                {
                    FacilityId = facilityId, IsReference = false, Paged = 25,
                    QueryType = FhirQueryType.Read, QueryParameters = new List<string>() { "Test " },
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } },
                    MeasureId = "TestMeasureId",
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                    {
                        new ResourceReferenceType { FacilityId = facilityId, QueryPhase = QueryPhase.Initial, ResourceType = "Patient", CreateDate = DateTime.UtcNow, ModifyDate = DateTime.UtcNow }
                    }
                }
            },
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var refSearchRT = new ReferenceResources { FacilityId = "test", QueryPhase = QueryPhase.Polling, ReferenceResource = "Test", ResourceId = Guid.NewGuid().ToString(), ResourceType = "test", CreateDate = DateTime.UtcNow };
        dbContext.ReferenceResources.Add(refSearchRT);
        await dbContext.SaveChangesAsync();
        log.ReferenceResources.Add(refSearchRT);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = (await queries.SearchAsync(new SearchDataAcquisitionLogRequest
        {
            FacilityId = facilityId,
            ResourceType = "Patient"
        })).Records.FirstOrDefault();

        Assert.NotNull(result);
        Assert.Equal(log.Id, result.Id);
    }

    [Fact]
    public async Task GetDataAcquisitionLogStatisticsByReportAsync_ReturnsStatistics()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var reportTrackingId = Guid.NewGuid();
        var facilityId = $"Stat_{tag}";

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            CompletionTimeMilliseconds = 100,
            QueryPhase = QueryPhase.Initial,
            QueryType = FhirQueryType.Read,
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            ResourceIds = new List<DataAcquisitionLogResourceId> { new() { ResourceId = "Patient/123" } },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log1);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var statistics = await queries.GetDataAcquisitionLogStatisticsByReportAsync(reportTrackingId.ToString());

        Assert.NotNull(statistics);
        Assert.Equal(1, statistics.TotalLogs);
        Assert.True(statistics.QueryPhaseCounts.ContainsKey(QueryPhase.Initial));
        Assert.True(statistics.RequestStatusCounts.ContainsKey(RequestStatus.Completed));
        Assert.True(statistics.ResourceTypeCounts.ContainsKey("Patient"));
        Assert.True(statistics.ResourceTypeCompletionTimeMilliseconds.ContainsKey("Patient"));
    }

    [Fact]
    public async Task GetDataAcquisitionLogStatisticsByReportAsync_IncludesCompleted()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var reportTrackingId = Guid.NewGuid();
        var facilityId = $"StatComp_{tag}";

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            CompletionTimeMilliseconds = 150,
            QueryPhase = QueryPhase.Initial,
            QueryType = FhirQueryType.Read,
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            ResourceIds = new List<DataAcquisitionLogResourceId>(),
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, FhirQueryResourceTypes = new List<FhirQueryResourceType> { new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var statistics = await queries.GetDataAcquisitionLogStatisticsByReportAsync(reportTrackingId.ToString());

        Assert.NotNull(statistics);
        Assert.Equal(1, statistics.TotalLogs);
        Assert.True(statistics.RequestStatusCounts.ContainsKey(RequestStatus.Completed));
        Assert.Equal(1, statistics.RequestStatusCounts[RequestStatus.Completed]);
    }

    [Fact]
    public async Task CheckIfReferenceResourceHasBeenSent_ReturnsTrueIfSent()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"RefSent_{tag}";
        var reportTrackingId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        var referenceId = $"Patient/{tag}";

        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Completed,
            PatientId = "Patient/123",
            ResourceIds = new List<DataAcquisitionLogResourceId> { new() { ResourceId = referenceId } },
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var hasBeenSent = await queries.CheckIfReferenceResourceHasBeenSent(referenceId, reportTrackingId.ToString(), facilityId, correlationId);

        Assert.True(hasBeenSent);
    }

    [Fact]
    public async Task GetFacilitiesWithPendingAndRetryableFailedRequests_ReturnsUniqueFacilities()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facility1 = $"Fac1_{tag}";
        var facility2 = $"Fac2_{tag}";
        var reportTrackingId1 = Guid.NewGuid();
        var reportTrackingId2 = Guid.NewGuid();
        var reportTrackingId3 = Guid.NewGuid();

        var log1 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facility1,
            Status = RequestStatus.Pending,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId1,
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId1, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log1);

        var log2 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facility2,
            Status = RequestStatus.Failed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId2,
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId2, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log2);

        var log3 = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facility1,
            Status = RequestStatus.Failed,
            CorrelationId = Guid.NewGuid().ToString(),
            ReportTrackingId = reportTrackingId3,
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = reportTrackingId3, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
        };
        dbContext.DataAcquisitionLogs.Add(log3);

        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var facilities = await queries.GetFacilitiesWithPendingAndRetryableFailedRequests();

        // Global query — just verify our facilities are included
        Assert.Contains(facility1, facilities);
        Assert.Contains(facility2, facilities);
    }

    [Fact]
    public async Task GetNextEligibleBatchForFacility_ReturnsBatchOfLogs()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"Batch_{tag}";

        for (int i = 1; i <= 5; i++)
        {
            var log = new DataAcquisitionLog
            {
                FhirVersion = "test",
                TraceId = Guid.NewGuid().ToString(),
                FacilityId = facilityId,
                Status = RequestStatus.Pending,
                CorrelationId = Guid.NewGuid().ToString(),
                ReportTrackingId = Guid.NewGuid(),
                PatientId = "Patient/123",
                ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow }
            };
            dbContext.DataAcquisitionLogs.Add(log);
        }
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var batch = await queries.GetNextEligibleBatchForFacility(facilityId, null, 3, new() { RequestStatus.Pending });

        Assert.Equal(3, batch.Count);
        Assert.All(batch, l => Assert.Equal(facilityId, l.FacilityId));
    }

    [Fact]
    public async Task GetNextEligibleBatchForFacility_ReturnsInitialLocationReferenceLog_WhenOrganizationLocationConfigurationIsActive()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"OrgLocBatch_{tag}";
        var correlationId = Guid.NewGuid().ToString();
        var reportTrackingId = Guid.NewGuid();
        var scheduledReport = new ScheduledReportEntity
        {
            ReportTrackingId = reportTrackingId,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow
        };

        dbContext.LocationConfigurations.Add(new OrganizationLocationConfiguration
        {
            FacilityId = facilityId,
            Description = "Active org-location mapping",
            IsActive = true
        });

        var pendingPrimary = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries =
            [
                new FhirQuery
                {
                    FacilityId = facilityId,
                    QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes =
                    [
                        new FhirQueryResourceType { ResourceType = Hl7.Fhir.Model.ResourceType.Condition }
                    ]
                }
            ]
        };

        var locationReferenceLog = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.Pending,
            QueryPhase = QueryPhase.Initial,
            ReferenceResourceType = Hl7.Fhir.Model.ResourceType.Location.ToString(),
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries =
            [
                new FhirQuery
                {
                    FacilityId = facilityId,
                    IsReference = true,
                    QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes =
                    [
                        new FhirQueryResourceType { ResourceType = Hl7.Fhir.Model.ResourceType.Location }
                    ]
                }
            ]
        };

        dbContext.DataAcquisitionLogs.AddRange(pendingPrimary, locationReferenceLog);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var batch = await queries.GetNextEligibleBatchForFacility(
            facilityId,
            null,
            10,
            [RequestStatus.Pending],
            DateTime.UtcNow);

        Assert.DoesNotContain(batch, log => log.Id == pendingPrimary.Id);
        Assert.Contains(batch, log => log.Id == locationReferenceLog.Id);
    }

    [Fact]
    public async Task FailStalledQueuedLogsAsync_FailsStalledLogs()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var stallMinutes = 15;
        var stalledDate = DateTime.UtcNow.AddMinutes(-(stallMinutes + 1));
        var recentDate = DateTime.UtcNow;

        var stalledLog = new DataAcquisitionLog
        {
            FacilityId = $"FailStalled_{tag}",
            Status = RequestStatus.Queued,
            ModifyDate = stalledDate
        };

        var recentLog = new DataAcquisitionLog
        {
            FacilityId = $"FailStalled_{tag}",
            Status = RequestStatus.Queued,
            ModifyDate = recentDate
        };

        dbContext.DataAcquisitionLogs.AddRange(stalledLog, recentLog);
        await dbContext.SaveChangesAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        int rowsAffected = await manager.FailStalledQueuedLogsAsync(stallMinutes);

        // Global operation — verify our specific logs instead of exact count
        Assert.True(rowsAffected >= 1);

        await dbContext.Entry(stalledLog).ReloadAsync();
        Assert.Equal(RequestStatus.Failed, stalledLog.Status);

        await dbContext.Entry(recentLog).ReloadAsync();
        Assert.Equal(RequestStatus.Queued, recentLog.Status);
    }

    [Fact]
    public async Task ResetStalledProcessingLogsAsync_ResetsStalledLogs()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var stallMinutes = 240;
        var stalledDate = DateTime.UtcNow.AddMinutes(-(stallMinutes + 1));
        var recentDate = DateTime.UtcNow;

        var stalledLog = new DataAcquisitionLog
        {
            FacilityId = $"ResetStalled_{tag}",
            Status = RequestStatus.Processing,
            ModifyDate = stalledDate
        };

        var recentLog = new DataAcquisitionLog
        {
            FacilityId = $"ResetStalled_{tag}",
            Status = RequestStatus.Processing,
            ModifyDate = recentDate
        };

        dbContext.DataAcquisitionLogs.AddRange(stalledLog, recentLog);
        await dbContext.SaveChangesAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        int rowsAffected = await manager.ResetStalledProcessingLogsAsync(stallMinutes);

        Assert.True(rowsAffected >= 1);

        await dbContext.Entry(stalledLog).ReloadAsync();
        Assert.Equal(RequestStatus.Pending, stalledLog.Status);

        await dbContext.Entry(recentLog).ReloadAsync();
        Assert.Equal(RequestStatus.Processing, recentLog.Status);
    }

    [Fact]
    public async Task FailStalledQueuedLogsAsync_RespectsMaxBatches()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var stallMinutes = 15;
        var stalledDate = DateTime.UtcNow.AddMinutes(-(stallMinutes + 1));

        var stalledLogs = Enumerable.Range(1, 250).Select(i => new DataAcquisitionLog
        {
            FacilityId = $"FailBatch_{tag}",
            Status = RequestStatus.Queued,
            ModifyDate = stalledDate
        }).ToList();

        dbContext.DataAcquisitionLogs.AddRange(stalledLogs);
        await dbContext.SaveChangesAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        int rowsAffected = await manager.FailStalledQueuedLogsAsync(stallMinutes, maxBatches: 1);

        Assert.True(rowsAffected >= 100);

        var testLogIds = stalledLogs.Select(l => l.Id).ToList();
        var failedCount = await dbContext.DataAcquisitionLogs
            .CountAsync(l => testLogIds.Contains(l.Id) && l.Status == RequestStatus.Failed);
        var stillQueuedCount = await dbContext.DataAcquisitionLogs
            .CountAsync(l => testLogIds.Contains(l.Id) && l.Status == RequestStatus.Queued);
        Assert.Equal(100, failedCount);
        Assert.Equal(150, stillQueuedCount);
    }

    [Fact]
    public async Task ResetStalledProcessingLogsAsync_RespectsMaxBatches()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var stallMinutes = 240;
        var stalledDate = DateTime.UtcNow.AddMinutes(-(stallMinutes + 1));

        var stalledLogs = Enumerable.Range(1, 250).Select(i => new DataAcquisitionLog
        {
            FacilityId = $"ResetBatch_{tag}",
            Status = RequestStatus.Processing,
            ModifyDate = stalledDate
        }).ToList();

        dbContext.DataAcquisitionLogs.AddRange(stalledLogs);
        await dbContext.SaveChangesAsync();

        var manager = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogManager>();

        int rowsAffected = await manager.ResetStalledProcessingLogsAsync(stallMinutes, maxBatches: 2);

        Assert.True(rowsAffected >= 200);

        var testLogIds = stalledLogs.Select(l => l.Id).ToList();
        var pendingCount = await dbContext.DataAcquisitionLogs
            .CountAsync(l => testLogIds.Contains(l.Id) && l.Status == RequestStatus.Pending);
        var stillProcessingCount = await dbContext.DataAcquisitionLogs
            .CountAsync(l => testLogIds.Contains(l.Id) && l.Status == RequestStatus.Processing);
        Assert.Equal(200, pendingCount);
        Assert.Equal(50, stillProcessingCount);
    }

    // ==================== GetNonTerminalDependencyResourceTypes Tests ====================

    [Fact]
    public async Task GetNonTerminalDependencyResourceTypes_EmptyInput_ReturnsEmpty()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var nullResult = await queries.GetNonTerminalDependencyResourceTypes(
            "corr", "facility", resourceTypes: null!, CancellationToken.None);
        var emptyResult = await queries.GetNonTerminalDependencyResourceTypes(
            "corr", "facility", new List<string>(), CancellationToken.None);
        var missingCorrResult = await queries.GetNonTerminalDependencyResourceTypes(
            "", "facility", ["Patient"], CancellationToken.None);
        var missingFacilityResult = await queries.GetNonTerminalDependencyResourceTypes(
            "corr", "  ", ["Patient"], CancellationToken.None);

        Assert.Empty(nullResult);
        Assert.Empty(emptyResult);
        Assert.Empty(missingCorrResult);
        Assert.Empty(missingFacilityResult);
    }

    [Fact]
    public async Task GetNonTerminalDependencyResourceTypes_InvalidResourceType_IsSkippedNonBlocking()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"DepInvalid_{tag}";
        var correlationId = Guid.NewGuid().ToString();

        // Seed a non-terminal Patient log so that — if the unparseable type were
        // to accidentally be broadened — it would blow up and fail the test.
        dbContext.DataAcquisitionLogs.Add(new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Queued,
            CorrelationId = correlationId,
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        });
        await dbContext.SaveChangesAsync();

        var result = await queries.GetNonTerminalDependencyResourceTypes(
            correlationId, facilityId,
            ["NotARealResource", "AlsoFake"],
            CancellationToken.None);

        // Unparseable types are skipped with a warning and treated as non-blocking.
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNonTerminalDependencyResourceTypes_MissingProducer_ReturnsEmptyForThatType()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"DepMissing_{tag}";
        var correlationId = Guid.NewGuid().ToString();

        // Seed an Encounter log only — nothing for Patient. Asking about Patient should
        // fail open (missing producer ≠ blocking).
        dbContext.DataAcquisitionLogs.Add(new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Queued,
            CorrelationId = correlationId,
            ReportTrackingId = Guid.NewGuid(),
            PatientId = "Patient/123",
            ScheduledReportEntity = new ScheduledReportEntity { ReportTrackingId = Guid.NewGuid(), StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow },
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new() { ResourceType = Hl7.Fhir.Model.ResourceType.Encounter } } }
            }
        });
        await dbContext.SaveChangesAsync();

        var result = await queries.GetNonTerminalDependencyResourceTypes(
            correlationId, facilityId,
            ["Patient"],
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetNonTerminalDependencyResourceTypes_MixedValidAndInvalid_OnlyNonTerminalValidReturned()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"DepMixed_{tag}";
        var correlationId = Guid.NewGuid().ToString();
        var reportTrackingId = Guid.NewGuid();
        var scheduledReport = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        // Patient log — non-terminal (Queued) → should block.
        dbContext.DataAcquisitionLogs.Add(new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Queued,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient } } }
            }
        });

        // Encounter log — terminal (Completed) → should NOT block.
        dbContext.DataAcquisitionLogs.Add(new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = RequestStatus.Completed,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new() { ResourceType = Hl7.Fhir.Model.ResourceType.Encounter } } }
            }
        });
        await dbContext.SaveChangesAsync();

        // Mix: non-terminal producer + terminal producer + missing producer + invalid type.
        var result = await queries.GetNonTerminalDependencyResourceTypes(
            correlationId, facilityId,
            ["Patient", "Encounter", "Condition", "Garbage"],
            CancellationToken.None);

        // Only Patient should come back — Encounter is terminal, Condition has no producer, Garbage is unparseable.
        Assert.Single(result);
        Assert.Equal("Patient", result[0]);
    }

    [Fact]
    public async Task GetNonTerminalDependencyResourceTypes_NotReportableAndConfigurationMissing_AreTerminalNonBlocking()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();
        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"DepTerminalNew_{tag}";
        var correlationId = Guid.NewGuid().ToString();
        var reportTrackingId = Guid.NewGuid();
        var scheduledReport = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        DataAcquisitionLog DependencyLog(Hl7.Fhir.Model.ResourceType resourceType, RequestStatus status) => new()
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            Status = status,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries = new List<FhirQuery>
            {
                new() { MeasureId = "test", FacilityId = facilityId, QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType> { new() { ResourceType = resourceType } } }
            }
        };

        dbContext.DataAcquisitionLogs.AddRange(
            // Patient — non-terminal (Queued) → blocks. Control proving the query returns something.
            DependencyLog(Hl7.Fhir.Model.ResourceType.Patient, RequestStatus.Queued),
            // Encounter — NotReportable (terminal) → must NOT block.
            DependencyLog(Hl7.Fhir.Model.ResourceType.Encounter, RequestStatus.NotReportable),
            // Condition — ConfigurationMissing (terminal) → must NOT block.
            DependencyLog(Hl7.Fhir.Model.ResourceType.Condition, RequestStatus.ConfigurationMissing));
        await dbContext.SaveChangesAsync();

        var result = await queries.GetNonTerminalDependencyResourceTypes(
            correlationId, facilityId,
            ["Patient", "Encounter", "Condition"],
            CancellationToken.None);

        // Only Patient blocks; NotReportable and ConfigurationMissing are terminal → non-blocking.
        Assert.Single(result);
        Assert.Equal("Patient", result[0]);
    }

    [Fact]
    public async Task GetNextEligibleBatchForFacility_NotReportableSibling_DoesNotBlockDependentLog()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var tag = Guid.NewGuid().ToString("N");
        var facilityId = $"OrgLocSeq_{tag}";

        dbContext.LocationConfigurations.Add(new OrganizationLocationConfiguration
        {
            FacilityId = facilityId,
            Description = "Active org-location mapping",
            IsActive = true
        });

        DataAcquisitionLog InitialLog(string correlationId, Guid reportTrackingId, ScheduledReportEntity scheduledReport,
            Hl7.Fhir.Model.ResourceType resourceType, RequestStatus status) => new()
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = status,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport,
            FhirQueries =
            [
                new FhirQuery { FacilityId = facilityId, QueryType = FhirQueryType.Search,
                    FhirQueryResourceTypes = [new FhirQueryResourceType { ResourceType = resourceType }] }
            ]
        };

        // Correlation A: a dependent (Condition) log whose only Encounter sibling is NotReportable (terminal)
        // → the dependent should be unblocked.
        var corrA = Guid.NewGuid().ToString();
        var reportA = Guid.NewGuid();
        var schedA = new ScheduledReportEntity { ReportTrackingId = reportA, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };
        var dependentA = InitialLog(corrA, reportA, schedA, Hl7.Fhir.Model.ResourceType.Condition, RequestStatus.Pending);
        var encounterSiblingA = InitialLog(corrA, reportA, schedA, Hl7.Fhir.Model.ResourceType.Encounter, RequestStatus.NotReportable);

        // Correlation B (control): identical shape but the Encounter sibling is still Queued (non-terminal)
        // → the dependent must remain blocked.
        var corrB = Guid.NewGuid().ToString();
        var reportB = Guid.NewGuid();
        var schedB = new ScheduledReportEntity { ReportTrackingId = reportB, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };
        var dependentB = InitialLog(corrB, reportB, schedB, Hl7.Fhir.Model.ResourceType.Condition, RequestStatus.Pending);
        var encounterSiblingB = InitialLog(corrB, reportB, schedB, Hl7.Fhir.Model.ResourceType.Encounter, RequestStatus.Queued);

        dbContext.DataAcquisitionLogs.AddRange(dependentA, encounterSiblingA, dependentB, encounterSiblingB);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var batch = await queries.GetNextEligibleBatchForFacility(
            facilityId, null, 50, [RequestStatus.Pending], DateTime.UtcNow);

        Assert.Contains(batch, l => l.Id == dependentA.Id);
        Assert.DoesNotContain(batch, l => l.Id == dependentB.Id);
    }

    [Fact]
    public async Task GetTailingMessages_WithNotReportable_ReturnsEligibleTailingMessages()
    {
        using var scope = _fixture.ServiceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DataAcquisitionDbContext>();

        var correlationId = Guid.NewGuid().ToString();
        var facilityId = $"TailNotReportable_{Guid.NewGuid():N}";
        var reportTrackingId = Guid.NewGuid();
        var scheduledReport = new ScheduledReportEntity { ReportTrackingId = reportTrackingId, StartDate = DateTime.UtcNow.AddDays(-1), EndDate = DateTime.UtcNow };

        // A correlation whose only log is NotReportable is fully terminal → the safety-net tail must fire.
        var log = new DataAcquisitionLog
        {
            FhirVersion = "test",
            TraceId = Guid.NewGuid().ToString(),
            FacilityId = facilityId,
            CorrelationId = correlationId,
            ReportTrackingId = reportTrackingId,
            Status = RequestStatus.NotReportable,
            TailSent = false,
            QueryPhase = QueryPhase.Initial,
            PatientId = "Patient/123",
            ScheduledReportEntity = scheduledReport
        };
        dbContext.DataAcquisitionLogs.Add(log);
        await dbContext.SaveChangesAsync();

        var queries = scope.ServiceProvider.GetRequiredService<IDataAcquisitionLogQueries>();

        var result = await queries.GetTailingMessages();

        var message = Assert.Single(result, m => m.CorrelationId == correlationId);
        Assert.Equal(facilityId, message.FacilityId);
        Assert.Contains(log.Id, message.LogIds);
    }
}

