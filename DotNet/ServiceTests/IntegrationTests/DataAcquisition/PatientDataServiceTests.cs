using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
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
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Moq;
using System.Linq.Expressions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;
using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;

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
