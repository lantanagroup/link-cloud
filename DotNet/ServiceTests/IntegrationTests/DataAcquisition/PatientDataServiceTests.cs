using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using Serilog;
using System.Linq.Expressions;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace IntegrationTests.DataAcquisition;

[Collection("DataAcquisitionIntegrationTests")]
[Trait("Category", "IntegrationTests")]
public class PatientDataServiceTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<PatientDataService>> _mockLogger;
    private readonly Mock<IFhirQueryConfigurationManager> _mockFhirQueryManager;
    private readonly Mock<IFhirQueryConfigurationQueries> _mockFhirQueryQueries;
    private readonly Mock<IQueryPlanManager> _mockQueryPlanManager;
    private readonly Mock<IQueryPlanQueries> _mockQueryPlanQueries;
    private readonly Mock<IProducer<string, ResourceAcquired>> _mockKafkaProducer;
    private readonly Mock<IQueryListProcessor> _mockQueryListProcessor;
    private readonly Mock<IReadFhirCommand> _mockReadFhirCommand;
    private readonly Mock<ISearchFhirCommand> _mockSearchFhirCommand;
    private readonly Mock<IDataAcquisitionLogManager> _mockLogManager;
    private readonly Mock<IReferenceResourcesManager> _mockReferenceResourcesManager;
    private readonly Mock<IDataAcquisitionLogQueries> _mockLogQueries;
    private readonly Mock<IReferenceResourceService> _mockRefService;
    private readonly Mock<IFhirApiService> _mockFhirApiService;
    private readonly Mock<IDistributedSemaphoreProvider> _mockDistributedSemaphoreProvider; // Added mock for the missing parameter
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IPatientCensusService> _mockPatientCensusService;

    private readonly PatientDataService _service;

    public PatientDataServiceTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<PatientDataService>>();
        _mockFhirQueryManager = new Mock<IFhirQueryConfigurationManager>();
        _mockFhirQueryQueries = new Mock<IFhirQueryConfigurationQueries>();
        _mockQueryPlanManager = new Mock<IQueryPlanManager>();
        _mockQueryPlanQueries = new Mock<IQueryPlanQueries>();
        _mockKafkaProducer = new Mock<IProducer<string, ResourceAcquired>>();
        _mockQueryListProcessor = new Mock<IQueryListProcessor>();
        _mockReadFhirCommand = new Mock<IReadFhirCommand>();
        _mockSearchFhirCommand = new Mock<ISearchFhirCommand>();
        _mockLogManager = new Mock<IDataAcquisitionLogManager>();
        _mockReferenceResourcesManager = new Mock<IReferenceResourcesManager>();
        _mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
        _mockRefService = new Mock<IReferenceResourceService>();
        _mockFhirApiService = new Mock<IFhirApiService>();
        _mockDistributedSemaphoreProvider = new Mock<IDistributedSemaphoreProvider>(); // Added mock for the missing parameter
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockPatientCensusService = new Mock<IPatientCensusService>();

        // Mock the semaphore and handle
        var mockSemaphore = new Mock<IDistributedSemaphore>();
        var mockHandle = new Mock<IDistributedSynchronizationHandle>();

        // Setup CreateSemaphore to return the mock semaphore
        _mockDistributedSemaphoreProvider
            .Setup(p => p.CreateSemaphore(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockSemaphore.Object);

        // Setup Acquire to return the mock handle
        mockSemaphore
            .Setup(s => s.Acquire(It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(mockHandle.Object);

        _service = new PatientDataService(
            _mockDatabase.Object,
            _mockLogger.Object,
            _mockFhirQueryQueries.Object,
            _mockQueryPlanQueries.Object,
            _mockQueryListProcessor.Object,
            _mockReadFhirCommand.Object,
            _mockLogManager.Object,
            _mockLogQueries.Object,
            _mockFhirApiService.Object,
            _mockDistributedSemaphoreProvider.Object, 
            _mockServiceProvider.Object,
            _mockPatientCensusService.Object
        );
    }

    [Fact]
    public async Task ValidateFacilityConnection_ShouldReturnResources_WhenValidRequest()
    {
        // Arrange
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<LantanaGroup.Link.Shared.Application.Models.ScheduledReport>
            {
                new LantanaGroup.Link.Shared.Application.Models.ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = "tracking-1"
                }
            }
        };

        var consumeResult = new ConsumeResult<string, DataAcquisitionRequested>
        {
            Message = new Message<string, DataAcquisitionRequested>
            {
                Value = dataAcqRequested
            }
        };

        var request = new GetPatientDataRequest
        {
            ConsumeResult = consumeResult,
            FacilityId = "facility-1",
            CorrelationId = "corr-1",
            QueryPlanType = QueryPlanType.Initial
        };
        var cancellationToken = CancellationToken.None;

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FacilityId = "facility-1",
            FhirServerBaseUrl = "http://example.com",
        };

        var queryPlan = new QueryPlanModel
        {
            FacilityId = "facility-1",
            Type = Frequency.Discharge,
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "q1", new ReferenceQueryConfig { ResourceType = ResourceType.Patient.ToString() } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fhirQueryConfig);

        _mockQueryPlanQueries
            .Setup(m => m.SearchAsync(It.IsAny<SearchQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<QueryPlanModel> { Records = [queryPlan] });

        _mockReadFhirCommand
            .Setup(cmd => cmd.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), cancellationToken))
            .ReturnsAsync(new Patient());

        _mockQueryListProcessor
            .Setup(p => p.ExecuteFacilityValidationRequest(
                It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                It.IsAny<GetPatientDataRequest>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<ScheduledReport>(), // Corrected argument type
                It.IsAny<QueryPlanModel>(),
                It.IsAny<List<string>>(),
                It.IsAny<string>(), // Corrected argument position
                cancellationToken)) // Corrected argument position
            .ReturnsAsync(new List<Resource> { new Patient() });

        // Act
        var result = await _service.ValidateFacilityConnection(request, cancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task ValidateFacilityConnection_ShouldThrowException_WhenRequestIsNull()
    {
        // Arrange
        GetPatientDataRequest request = null;
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ValidateFacilityConnection(request, cancellationToken));
    }

    [Fact]
    public async Task CreateLogEntries_ShouldCallLogManager_WhenValidRequest()
    {
        // Arrange
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<LantanaGroup.Link.Shared.Application.Models.ScheduledReport>
            {
                new LantanaGroup.Link.Shared.Application.Models.ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = "tracking-1"
                }
            }
        };

        var consumeResult = new ConsumeResult<string, DataAcquisitionRequested>
        {
            Message = new Message<string, DataAcquisitionRequested>
            {
                Value = dataAcqRequested
            }
        };

        var request = new GetPatientDataRequest
        {
            ConsumeResult = consumeResult,
            FacilityId = "facility-1",
            CorrelationId = "corr-1",
            QueryPlanType = QueryPlanType.Initial
        };
        var cancellationToken = CancellationToken.None;

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FacilityId = "facility-1",
            FhirServerBaseUrl = "http://example.com",
        };

        var queryPlan = new QueryPlanModel
        {
            FacilityId = "facility-1",
            Type = Frequency.Discharge,
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                { "q1", new ReferenceQueryConfig { ResourceType = ResourceType.Patient.ToString() } }
            },
            SupplementalQueries = new Dictionary<string, IQueryConfig>()
        };

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fhirQueryConfig);

        _mockQueryPlanQueries
            .Setup(m => m.SearchAsync(It.IsAny<SearchQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<QueryPlanModel> { Records = [queryPlan] });

        _mockLogManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken))
            .ReturnsAsync(new DataAcquisitionLogModel());

        _mockQueryListProcessor
            .Setup(p => p.Process(
                It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                It.IsAny<GetPatientDataRequest>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<QueryPlanModel>(),
                It.IsAny<List<ResourceReferenceType>>(),
                It.IsAny<string>(),
                It.IsAny<ScheduledReport>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.CreateLogEntries(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken), Times.Once);
    }

    [Fact]
    public async Task CreateLogEntries_ShouldThrowException_WhenRequestIsNull()
    {
        // Arrange
        GetPatientDataRequest request = null;
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateLogEntries(request, cancellationToken));
    }

    [Fact]
    public async Task ExecuteLogRequest_ShouldCallLogManager_WhenValidRequest()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Ready,
            FhirQueries = new List<FhirQuery>
        {
            new FhirQuery
            {
                QueryType = FhirQueryType.Read,
                FhirQueryResourceTypes = new List<FhirQueryResourceType>
                {
                    new FhirQueryResourceType() { ResourceType = Hl7.Fhir.Model.ResourceType.Patient }
                },
                QueryParameters = new List<string>(),
                ResourceReferenceTypes = new List<ResourceReferenceType>()
            }
        },
            ScheduledReport = new ScheduledReport(),
            PatientId = "patient-1",
            CorrelationId = "corr-1"
        };

        var model = DataAcquisitionLogModel.FromDomain(log);

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FacilityId = "facilityId",
            FhirServerBaseUrl = "http://example.com"
        };

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(model);

        _mockLogManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .ReturnsAsync(model);

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        // ADD THIS SETUP - Mock the ExecuteRead method to return a list of IDs
        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<Hl7.Fhir.Model.ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/patient-1" });

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteLogRequest_ShouldThrowException_WhenRequestIsNull()
    {
        // Arrange
        AcquisitionRequest request = null;
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ExecuteLogRequest(request, cancellationToken));
    }

    [Fact]
    public async Task ExecuteLogRequest_WhenSearchQueryHasOnlyEmptyIdsIn_IdParameter_SkipsFetchAndMarksCompletedWithNote()
    {
        // Arrange
        var facilityId = "facility-001";
        var logId = 42L;
        var request = new AcquisitionRequest(logId, facilityId);
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = logId,
            FacilityId = facilityId,
            Status = RequestStatus.Ready,
            IsCensus = false,
            Notes = new List<string>(),
            FhirQueries = new List<FhirQuery>
        {
            new FhirQuery
            {
                QueryType = FhirQueryType.Search,
                QueryParameters = new List<string>
                {
                    "_id=",               // empty value
                    "_id=   ,  ,",        // only whitespace and commas
                    "_id=actual-id-123"   // one real ID to make parsing more interesting
                },
                FhirQueryResourceTypes = new List<FhirQueryResourceType>
                {
                    new FhirQueryResourceType { ResourceType = ResourceType.Observation }
                }
            }
        }
        };

        var logModel = DataAcquisitionLogModel.FromDomain(log);

        var fhirConfig = new FhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "https://fhir.example.com"
        };

        // Mock dependencies
        _mockLogQueries
            .Setup(q => q.GetAsync(logId, cancellationToken))
            .ReturnsAsync(logModel);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync(facilityId, cancellationToken))
            .ReturnsAsync(fhirConfig);

        // Critical: We expect ExecuteSearch to be called exactly once for the valid ID,
        // but we will verify it is called only for the non-empty case later if needed.
        // For this test we actually want to prove that when ALL IDs are empty → NO call

        // So let's adjust the parameters to have ONLY empty/whitespace IDs
        log.FhirQueries.First().QueryParameters = new List<string>
    {
        "_id=",
        "_id=,,   ,",
        "_id=     "
    };

        // Update the model after changing the entity
        logModel = DataAcquisitionLogModel.FromDomain(log);

        _mockLogQueries
            .Setup(q => q.GetAsync(logId, cancellationToken))
            .ReturnsAsync(logModel);

        // Expect exactly ONE update to Processing, then ONE final update to Completed
        var updateCallCount = 0;
        _mockLogManager
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((model, _) =>
            {
                updateCallCount++;
                if (updateCallCount == 1)
                {
                    Assert.Equal(RequestStatus.Processing, model.Status);
                }
                else if (updateCallCount == 2)
                {
                    Assert.Equal(RequestStatus.Skipped, model.Status);
                    Assert.Contains(model.Notes, n =>
                        n.Contains("No IDs found in _id query parameter for Search FHIR query. Marking log as Completed."));
                }
            })
            .ReturnsAsync(logModel);

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(
            m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken),
            Times.Exactly(2)); // Processing → Completed

        // Most important: ExecuteSearch should NEVER be called when no valid IDs exist
        _mockFhirApiService.Verify(
            s => s.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<ResourceType>(),
                cancellationToken),
            Times.Never);

        _mockFhirApiService.Verify(
            s => s.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                cancellationToken),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteLogRequest_WhenSearchQueryHasMixedValidAndEmptyIds_FetchesValidIdsAndDoesNotAddNoIdsNote()
    {
        // Arrange
        var facilityId = "facility-001";
        var logId = 99L;
        var request = new AcquisitionRequest(logId, facilityId);
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = logId,
            FacilityId = facilityId,
            Status = RequestStatus.Ready,
            IsCensus = false,
            Notes = new List<string>(),
            FhirQueries = new List<FhirQuery>
        {
            new FhirQuery
            {
                QueryType = FhirQueryType.Search,
                QueryParameters = new List<string>
                {
                    "_id=obs-100,obs-200",           // valid IDs
                    "_id=,   ,",                      // empty + whitespace + commas
                    "_id=obs-300",                    // another valid
                    "_id=     "                       // only whitespace
                },
                FhirQueryResourceTypes = new List<FhirQueryResourceType>
                {
                    new FhirQueryResourceType { ResourceType = ResourceType.Observation }
                }
            }
        }
        };

        var logModel = DataAcquisitionLogModel.FromDomain(log);

        var fhirConfig = new FhirQueryConfigurationModel
        {
            FacilityId = facilityId,
            FhirServerBaseUrl = "https://fhir.example.com"
        };

        // Setup mocks
        _mockLogQueries
            .Setup(q => q.GetAsync(logId, cancellationToken))
            .ReturnsAsync(logModel);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync(facilityId, cancellationToken))
            .ReturnsAsync(fhirConfig);

        // Capture updates to verify final state and that "No IDs" note is NOT added
        _mockLogManager
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((model, _) =>
            {
                // Final update should be to Completed
                if (model.Status == RequestStatus.Completed)
                {
                    // This note must NOT be present
                    var hasNoIdsNote = model.Notes?.Any(n =>
                        n.Contains("No IDs found in _id query parameter for Search FHIR query") &&
                        n.Contains("Marking log as Completed")) ?? false;

                    Assert.False(hasNoIdsNote, "The 'No IDs found' note should not be added when valid IDs exist.");
                }
            })
            .ReturnsAsync(logModel);

        // Expect ExecuteSearch to be called once (for Observation)
        _mockFhirApiService
            .Setup(s => s.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                ResourceType.Observation,
                cancellationToken))
            .ReturnsAsync(new List<string> { "obs-100", "obs-200", "obs-300" }) // simulate returned IDs
            .Verifiable(); // allows .Verify() later

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        _mockFhirApiService.Verify(
            s => s.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                ResourceType.Observation,
                cancellationToken),
            Times.Once,
            "ExecuteSearch should be called when at least one valid ID exists in _id parameter.");

        _mockLogManager.Verify(
            m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken),
            Times.AtLeast(2)); // Processing + Completed (possibly more if other logic runs)

        // Final confirmation: log completed successfully without the "no IDs" note
        _mockLogManager.Verify(
            m => m.UpdateAsync(
                It.Is<UpdateDataAcquisitionLogModel>(u =>
                    u.Status == RequestStatus.Completed &&
                    (u.Notes == null || !u.Notes.Any(n => n.Contains("No IDs found in _id query parameter")))),
                cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteLogRequest_ShouldAccumulateResourceIds_FromMultipleFhirQueries()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facility-1");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facility-1",
            PatientId = "Patient/123",
            Status = RequestStatus.Ready,
            CorrelationId = "corr-1",
            FhirQueries = new List<FhirQuery>
    {
        new FhirQuery
        {
            QueryType = FhirQueryType.Read,
            FhirQueryResourceTypes = new List<FhirQueryResourceType>
            {
                new() { ResourceType = ResourceType.Patient }
            }
        },
        new FhirQuery
        {
            QueryType = FhirQueryType.Search,
            FhirQueryResourceTypes = new List<FhirQueryResourceType>
            {
                new() { ResourceType = ResourceType.Observation }
            },
            QueryParameters = new List<string> { "patient=Patient/123" }
        },
        new FhirQuery
        {
            QueryType = FhirQueryType.Read,
            FhirQueryResourceTypes = new List<FhirQueryResourceType>
            {
                new() { ResourceType = ResourceType.Encounter }
            }
        }
    },
            ScheduledReport = new ScheduledReport()
        };

        var model = DataAcquisitionLogModel.FromDomain(log);

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(model);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync("facility-1", cancellationToken))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = "facility-1" });

        // Mock three different queries returning different IDs
        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Patient)),
                ResourceType.Patient,
                It.IsAny<FhirQueryConfigurationModel>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/123" });

        _mockFhirApiService
            .Setup(x => x.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Observation)),
                It.IsAny<FhirQueryConfigurationModel>(),
                ResourceType.Observation,
                cancellationToken))
            .ReturnsAsync(new[] { "Observation/obs1", "Observation/obs2" });

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Encounter)),
                ResourceType.Encounter,
                It.IsAny<FhirQueryConfigurationModel>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Encounter/enc1" });

        _mockLogManager
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .ReturnsAsync(model)
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((updateModel, _) =>
            {
                // Capture the final log state
                model.ResourceAcquiredIds = updateModel.ResourceAcquiredIds;
                model.Status = updateModel.Status;
            });

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert - All IDs from all queries must be present
        _mockLogManager.Verify(m => m.UpdateAsync(
            It.Is<UpdateDataAcquisitionLogModel>(u =>
                u.ResourceAcquiredIds != null &&
                u.ResourceAcquiredIds.Count == 4 &&
                u.ResourceAcquiredIds.Contains("Patient/123") &&
                u.ResourceAcquiredIds.Contains("Observation/obs1") &&
                u.ResourceAcquiredIds.Contains("Observation/obs2") &&
                u.ResourceAcquiredIds.Contains("Encounter/enc1") &&
                u.Status == RequestStatus.Completed
            ),
            cancellationToken),
            Times.Once);
    }
}
