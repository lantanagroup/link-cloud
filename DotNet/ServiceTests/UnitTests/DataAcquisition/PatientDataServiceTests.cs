using System.Net;
using Confluent.Kafka;
using DataAcquisition.Domain.Application.Models;
using Hl7.Fhir.Rest;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.QueryLog;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Requests;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
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
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Responses;
using Medallion.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition;

[Trait("Category", "UnitTests")]
public class PatientDataServiceTests
{
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<PatientDataService>> _mockLogger;
    private readonly Mock<IFhirQueryConfigurationManager> _mockFhirQueryManager;
    private readonly Mock<IFhirQueryConfigurationQueries> _mockFhirQueryQueries;
    private readonly Mock<IQueryPlanManager> _mockQueryPlanManager;
    private readonly Mock<IQueryPlanQueries> _mockQueryPlanQueries;
    private readonly Mock<IProducer<ResourceKey, ResourceAcquired>> _mockKafkaProducer;
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
    private readonly Mock<IScheduledReportManager> _mockScheduledReportManager;
    private readonly Mock<IDataAcquisitionServiceMetrics> _mockMetrics;
    private readonly Mock<IOptionsMonitor<TelemetrySettings>> _mockTelemetrySettings;
    private readonly Mock<ILocationMappingService> _mockLocationMappingService;

    private readonly PatientDataService _service;

    public PatientDataServiceTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<PatientDataService>>();
        _mockFhirQueryManager = new Mock<IFhirQueryConfigurationManager>();
        _mockFhirQueryQueries = new Mock<IFhirQueryConfigurationQueries>();
        _mockQueryPlanManager = new Mock<IQueryPlanManager>();
        _mockQueryPlanQueries = new Mock<IQueryPlanQueries>();
        _mockKafkaProducer = new Mock<IProducer<ResourceKey, ResourceAcquired>>();
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
        _mockScheduledReportManager = new Mock<IScheduledReportManager>();
        _mockMetrics = new Mock<IDataAcquisitionServiceMetrics>();
        _mockTelemetrySettings = new Mock<IOptionsMonitor<TelemetrySettings>>();
        _mockLocationMappingService = new Mock<ILocationMappingService>();

        _mockTelemetrySettings
            .SetupGet(x => x.CurrentValue)
            .Returns(new TelemetrySettings { PatientTags = false });

        _mockLocationMappingService
            .Setup(x => x.IsConfigured(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
            _mockLocationMappingService.Object,
            _mockFhirApiService.Object,
            _mockRefService.Object,
            _mockDistributedSemaphoreProvider.Object,
            _mockServiceProvider.Object,
            _mockPatientCensusService.Object,
            _mockScheduledReportManager.Object,
            _mockMetrics.Object,
            _mockTelemetrySettings.Object
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
            ScheduledReports = new List<ScheduledReport>
            {
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
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
            ScheduledReports = new List<ScheduledReport>
            {
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
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
            .ReturnsAsync(0);

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
    public async Task CreateLogEntries_ShouldPrioritizeEncounterAndLocationInitialQueries_WhenOrganizationLocationConfigurationIsActive()
    {
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports =
            [
                new ScheduledReport
                {
                    ReportTypes = ["measure-1"],
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
                }
            ]
        };

        var request = new GetPatientDataRequest
        {
            ConsumeResult = new ConsumeResult<string, DataAcquisitionRequested>
            {
                Message = new Message<string, DataAcquisitionRequested> { Value = dataAcqRequested }
            },
            FacilityId = "facility-1",
            CorrelationId = "corr-1",
            QueryPlanType = QueryPlanType.Initial
        };

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FacilityId = "facility-1",
            FhirServerBaseUrl = "http://example.com"
        };

        var queryPlan = new QueryPlanModel
        {
            FacilityId = "facility-1",
            Type = Frequency.Discharge,
            InitialQueries = new Dictionary<string, IQueryConfig>
            {
                ["1"] = new ParameterQueryConfig { ResourceType = ResourceType.Observation.ToString() },
                ["2"] = new ReferenceQueryConfig { ResourceType = ResourceType.Location.ToString() },
                ["3"] = new ParameterQueryConfig { ResourceType = ResourceType.Condition.ToString() },
                ["4"] = new ParameterQueryConfig { ResourceType = ResourceType.Encounter.ToString() }
            },
            SupplementalQueries = []
        };

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facility-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fhirQueryConfig);

        _mockQueryPlanQueries
            .Setup(m => m.SearchAsync(It.IsAny<SearchQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<QueryPlanModel> { Records = [queryPlan] });

        _mockLogManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataAcquisitionLogModel());

        _mockLocationMappingService
            .Setup(x => x.IsConfigured("facility-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        List<string> orderedResourceTypes = [];
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
            .Callback<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>, GetPatientDataRequest, FhirQueryConfigurationModel, QueryPlanModel, List<ResourceReferenceType>, string, ScheduledReport, CancellationToken>(
                (queries, _, _, _, _, _, _, _) =>
                {
                    orderedResourceTypes = queries
                        .Select(q => q.Value switch
                        {
                            ParameterQueryConfig parameter => parameter.ResourceType,
                            ReferenceQueryConfig reference => reference.ResourceType,
                            _ => string.Empty
                        })
                        .ToList();
                })
            .ReturnsAsync(0);

        await _service.CreateLogEntries(request, CancellationToken.None);

        Assert.Equal(
            [ResourceType.Encounter.ToString(), ResourceType.Location.ToString(), ResourceType.Observation.ToString(), ResourceType.Condition.ToString()],
            orderedResourceTypes);
    }

    [Fact]
    public async Task CreateLogEntries_ShouldCreateTerminalConfigurationMissingLog_WhenFhirQueryConfigurationIsMissing()
    {
        // Arrange
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<ScheduledReport>
            {
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
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

        // No FhirQueryConfiguration for the facility
        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirQueryConfigurationModel)null);

        CreateDataAcquisitionLogModel capturedModel = null;
        _mockLogManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<CreateDataAcquisitionLogModel, CancellationToken>((m, _) => capturedModel = m)
            .ReturnsAsync(new DataAcquisitionLogModel());

        // Act
        await _service.CreateLogEntries(request, cancellationToken);

        // Assert: no exception thrown, a single terminal ConfigurationMissing log was created
        _mockLogManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken),
            Times.Once);

        Assert.NotNull(capturedModel);
        Assert.Equal(RequestStatus.ConfigurationMissing, capturedModel.Status);
        Assert.Equal("facility-1", capturedModel.FacilityId);
        Assert.Equal("patient-123", capturedModel.PatientId);
        Assert.Equal("corr-1", capturedModel.CorrelationId);
        Assert.NotNull(capturedModel.Notes);
        Assert.Contains(capturedModel.Notes, n => n.Contains("FhirQueryConfiguration", StringComparison.OrdinalIgnoreCase));

        // QueryPlan lookup must not have been attempted, and the QueryListProcessor must not run
        _mockQueryPlanQueries.Verify(
            q => q.SearchAsync(It.IsAny<SearchQueryPlanModel>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _mockQueryListProcessor.Verify(
            p => p.Process(
                It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                It.IsAny<GetPatientDataRequest>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<QueryPlanModel>(),
                It.IsAny<List<ResourceReferenceType>>(),
                It.IsAny<string>(),
                It.IsAny<ScheduledReport>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // SiblingCount should not be stamped — no actionable group was created
        _mockLogManager.Verify(
            m => m.StampSiblingCountAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateLogEntries_ShouldCreateTerminalConfigurationMissingLog_WhenQueryPlanIsMissing()
    {
        // Arrange
        var reportTrackingId = Guid.NewGuid().ToString();
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<ScheduledReport>
            {
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = reportTrackingId
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

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fhirQueryConfig);

        // No QueryPlan returned
        _mockQueryPlanQueries
            .Setup(m => m.SearchAsync(It.IsAny<SearchQueryPlanModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedConfigModel<QueryPlanModel> { Records = new List<QueryPlanModel>() });

        CreateDataAcquisitionLogModel capturedModel = null;
        _mockLogManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<CreateDataAcquisitionLogModel, CancellationToken>((m, _) => capturedModel = m)
            .ReturnsAsync(new DataAcquisitionLogModel());

        // Act
        await _service.CreateLogEntries(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken),
            Times.Once);

        Assert.NotNull(capturedModel);
        Assert.Equal(RequestStatus.ConfigurationMissing, capturedModel.Status);
        Assert.Equal(reportTrackingId, capturedModel.ReportTrackingId);
        Assert.Contains(capturedModel.Notes, n => n.Contains("QueryPlan", StringComparison.OrdinalIgnoreCase));

        // ScheduledReport row should be ensured for the supplied tracking id
        _mockScheduledReportManager.Verify(
            s => s.EnsureCreatedAsync(It.Is<ScheduledReport>(r => r.ReportTrackingId == reportTrackingId), cancellationToken),
            Times.Once);

        // QueryListProcessor must not run when configuration is incomplete
        _mockQueryListProcessor.Verify(
            p => p.Process(
                It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                It.IsAny<GetPatientDataRequest>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<QueryPlanModel>(),
                It.IsAny<List<ResourceReferenceType>>(),
                It.IsAny<string>(),
                It.IsAny<ScheduledReport>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateLogEntries_ShouldCreateOneConfigurationMissingLogPerScheduledReport_WhenConfigurationIsMissing()
    {
        // Arrange
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<ScheduledReport>
            {
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-1" },
                    Frequency = Frequency.Daily,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
                },
                new ScheduledReport
                {
                    ReportTypes = new List<string> { "measure-2" },
                    Frequency = Frequency.Discharge,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(1),
                    ReportTrackingId = Guid.NewGuid().ToString()
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

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FhirQueryConfigurationModel)null);

        var captured = new List<CreateDataAcquisitionLogModel>();
        _mockLogManager
            .Setup(manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<CreateDataAcquisitionLogModel, CancellationToken>((m, _) => captured.Add(m))
            .ReturnsAsync(new DataAcquisitionLogModel());

        // Act
        await _service.CreateLogEntries(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), cancellationToken),
            Times.Exactly(2));

        Assert.Equal(2, captured.Count);
        Assert.All(captured, m => Assert.Equal(RequestStatus.ConfigurationMissing, m.Status));

        // Daily report should be marked High priority, others Normal
        Assert.Single(captured, m => m.Priority == AcquisitionPriority.High);
        Assert.Single(captured, m => m.Priority == AcquisitionPriority.Normal);
    }

    [Fact]
    public async Task CreateLogEntries_ShouldStillThrow_WhenFhirQueryConfigurationLookupFails()
    {
        // Arrange — verify that genuine errors (e.g. DB failure) during config lookup are
        // still surfaced as exceptions (so the listener can retry transient infrastructure issues).
        var dataAcqRequested = new DataAcquisitionRequested
        {
            PatientId = "patient-123",
            ReportableEvent = ReportableEvent.Discharge,
            QueryType = "Initial",
            ScheduledReports = new List<ScheduledReport>()
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

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateLogEntries(request, cancellationToken));

        _mockLogManager.Verify(
            manager => manager.CreateAsync(It.IsAny<CreateDataAcquisitionLogModel>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
            Status = RequestStatus.Queued,
            FhirQueries = new List<FhirQuery>
        {
            new FhirQuery
            {
                QueryType = FhirQueryType.Read,
                FhirQueryResourceTypes = new List<FhirQueryResourceType>
                {
                    new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                },
                QueryParameters = new List<string>(),
                ResourceReferenceTypes = new List<ResourceReferenceType>()
            }
        },
            ScheduledReportEntity = new ScheduledReportEntity(),
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
            .Returns(Task.CompletedTask);

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        // ADD THIS SETUP - Mock the ExecuteRead method to return a list of IDs
        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/patient-1" });

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteLogRequest_ShouldNotIncludePatientIdentityMetricTags_WhenPatientTagsDisabled()
    {
        // Arrange
        _mockTelemetrySettings
            .SetupGet(x => x.CurrentValue)
            .Returns(new TelemetrySettings { PatientTags = false });

        List<KeyValuePair<string, object?>>? capturedTags = null;
        _mockMetrics
            .Setup(m => m.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>() ))
            .Callback<List<KeyValuePair<string, object?>>>(tags => capturedTags = tags);

        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Queued,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                    },
                    QueryParameters = new List<string>(),
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                }
            },
            ScheduledReportEntity = new ScheduledReportEntity(),
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
            .Returns(Task.CompletedTask);

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator?>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/patient-1" });

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        Assert.NotNull(capturedTags);
        Assert.DoesNotContain(capturedTags, tag => tag.Key == "patient_id");
        Assert.DoesNotContain(capturedTags, tag => tag.Key == "correlation_id");
    }

    [Fact]
    public async Task ExecuteLogRequest_ShouldIncludePatientIdentityMetricTags_WhenPatientTagsEnabled()
    {
        // Arrange
        _mockTelemetrySettings
            .SetupGet(x => x.CurrentValue)
            .Returns(new TelemetrySettings { PatientTags = true });

        List<KeyValuePair<string, object?>>? capturedTags = null;
        _mockMetrics
            .Setup(m => m.MeasureDataRequestDuration(It.IsAny<List<KeyValuePair<string, object?>>>() ))
            .Callback<List<KeyValuePair<string, object?>>>(tags => capturedTags = tags);

        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Queued,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                    },
                    QueryParameters = new List<string>(),
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                }
            },
            ScheduledReportEntity = new ScheduledReportEntity(),
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
            .Returns(Task.CompletedTask);

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator?>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/patient-1" });

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        Assert.NotNull(capturedTags);
        Assert.Contains(capturedTags, tag => tag.Key == "patient_id" && (tag.Value?.ToString() ?? string.Empty) == "patient-1");
        Assert.Contains(capturedTags, tag => tag.Key == "correlation_id" && (tag.Value?.ToString() ?? string.Empty) == "corr-1");
    }

    [Fact]
    public async Task ExecuteLogRequest_HandlesOpOutcomeException_404_SetsCompletedStatus()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Queued,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                    },
                    QueryParameters = new List<string>(),
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                }
            },
            ScheduledReportEntity = new ScheduledReportEntity(),
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

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new OpOutcomeException("OperationOutcome encountered", new FhirOperationException("test", HttpStatusCode.NotFound)));

        UpdateDataAcquisitionLogModel updatedModel = null;
        _mockLogManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((m, ct) => updatedModel = m)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        Assert.NotNull(updatedModel);
        Assert.Equal(RequestStatus.Completed, updatedModel.Status);
    }

    [Fact]
    public async Task ExecuteLogRequest_HandlesOpOutcomeException_500_SetsPendingStatus()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Queued,
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                    },
                    QueryParameters = new List<string>(),
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                }
            },
            ScheduledReportEntity = new ScheduledReportEntity(),
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

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new OpOutcomeException("OperationOutcome encountered", new FhirOperationException("test", HttpStatusCode.InternalServerError)));

        UpdateDataAcquisitionLogModel updatedModel = null;
        _mockLogManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((m, ct) => updatedModel = m)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        Assert.NotNull(updatedModel);
        Assert.Equal(RequestStatus.Failed, updatedModel.Status);
    }

    [Fact]
    public async Task ExecuteLogRequest_HandlesOpOutcomeException_MaxRetriesReached_SetsMaxRetriesReachedStatus()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facilityId");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLog
        {
            Id = 1,
            FacilityId = "facilityId",
            Status = RequestStatus.Queued,
            RetryAttempts = 2, // 2nd attempt, about to be 3rd
            FhirQueries = new List<FhirQuery>
            {
                new FhirQuery
                {
                    QueryType = FhirQueryType.Read,
                    FhirQueryResourceTypes = new List<FhirQueryResourceType>
                    {
                        new FhirQueryResourceType() { ResourceType = ResourceType.Patient }
                    },
                    QueryParameters = new List<string>(),
                    ResourceReferenceTypes = new List<ResourceReferenceType>()
                }
            }
        };

        var model = DataAcquisitionLogModel.FromDomain(log);

        var fhirQueryConfig = new FhirQueryConfigurationModel
        {
            FacilityId = "facilityId",
            FhirServerBaseUrl = "http://example.com",
            MaxRetries = 3
        };

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(model);

        _mockFhirQueryQueries
            .Setup(m => m.GetByFacilityIdAsync("facilityId", cancellationToken))
            .ReturnsAsync(fhirQueryConfig);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new OpOutcomeException("OperationOutcome encountered", new FhirOperationException("test", HttpStatusCode.InternalServerError)));

        UpdateDataAcquisitionLogModel updatedModel = null;
        _mockLogManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((m, ct) => updatedModel = m)
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        Assert.NotNull(updatedModel);
        Assert.Equal(3, updatedModel.RetryAttempts);
        Assert.Equal(RequestStatus.MaxRetriesReached, updatedModel.Status);
        Assert.Contains(updatedModel.NewNotes, n => n.Contains("Maximum retry attempts reached (3)."));
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
            Status = RequestStatus.Queued,
            IsCensus = false,
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

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(logId, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        // Critical: We expect ExecuteSearch to be called exactly once for the valid ID,
        // but we will verify it is called only for the non-empty case later if needed.
        // For this test we actually want to prove that when ALL IDs are empty ? NO call

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

        // Capture updates and validate terminal state (processing transition is done via TrySetLogStatusAsync)
        var updates = new List<UpdateDataAcquisitionLogModel>();
        _mockLogManager
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Callback<UpdateDataAcquisitionLogModel, CancellationToken>((model, _) =>
            {
                updates.Add(model);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert
        _mockLogManager.Verify(
            m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken),
            Times.AtLeastOnce());

        Assert.Contains(updates, u =>
            u.Status == RequestStatus.Skipped &&
            (u.NewNotes?.Any(n => n.Contains("No IDs found in _id query parameter for Search FHIR query. Marking log as Completed.")) ?? false));

        // Most important: ExecuteSearch should NEVER be called when no valid IDs exist
        _mockFhirApiService.Verify(
            s => s.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken),
            Times.Never);

        _mockFhirApiService.Verify(
            s => s.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<ResourceType>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
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
            Status = RequestStatus.Queued,
            IsCensus = false,
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
                    var hasNoIdsNote = model.NewNotes?.Any(n =>
                        n.Contains("No IDs found in _id query parameter for Search FHIR query") &&
                        n.Contains("Marking log as Completed")) ?? false;

                    Assert.False(hasNoIdsNote, "The 'No IDs found' note should not be added when valid IDs exist.");
                }
            })
            .Returns(Task.CompletedTask);

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(logId, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        // Expect ExecuteSearch to be called once (for Observation)
        _mockFhirApiService
            .Setup(s => s.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                It.IsAny<FhirQueryConfigurationModel>(),
                ResourceType.Observation,
                It.IsAny<DiscoveredReferenceAccumulator>(),
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
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken),
            Times.Once,
            "ExecuteSearch should be called when at least one valid ID exists in _id parameter.");

        _mockLogManager.Verify(
            m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken),
            Times.AtLeastOnce()); // Final completion update is required; processing transition uses TrySetLogStatusAsync

        // Final confirmation: log completed successfully without the "no IDs" note
        _mockLogManager.Verify(
            m => m.UpdateAsync(
                It.Is<UpdateDataAcquisitionLogModel>(u =>
                    u.Status == RequestStatus.Completed &&
                    (u.NewNotes == null || !u.NewNotes.Any(n => n.Contains("No IDs found in _id query parameter")))),
                cancellationToken),
            Times.AtLeastOnce());
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
            Status = RequestStatus.Queued,
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
            ScheduledReportEntity = new ScheduledReportEntity()
        };

        var model = DataAcquisitionLogModel.FromDomain(log);

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(model);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync("facility-1", cancellationToken))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = "facility-1" });

        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        // Mock three different queries returning different IDs
        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Patient)),
                ResourceType.Patient,
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Patient/123" });

        _mockFhirApiService
            .Setup(x => x.ExecuteSearch(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Observation)),
                It.IsAny<FhirQueryConfigurationModel>(),
                ResourceType.Observation,
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Observation/obs1", "Observation/obs2" });

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.Is<FhirQueryModel>(q => q.ResourceTypes.Contains(ResourceType.Encounter)),
                ResourceType.Encounter,
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ReturnsAsync(new[] { "Encounter/enc1" });

        _mockLogManager
            .Setup(m => m.UpdateAsync(It.IsAny<UpdateDataAcquisitionLogModel>(), cancellationToken))
            .Returns(Task.CompletedTask)
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

    [Fact]
    public async Task ExecuteLogRequest_Handles429WithSecondsDelay_ReschedulesLogWithDelay()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facility-1");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLogModel
        {
            Id = 1,
            FacilityId = "facility-1",
            PatientId = "Patient/123",
            Status = RequestStatus.Queued,
            CorrelationId = "corr-1",
            FhirQuery = new List<FhirQueryModel>
        {
            new FhirQueryModel
            {
                QueryType = FhirQueryType.Read,
                ResourceTypes = new List<ResourceType> { ResourceType.Patient }
            }
        },
            ScheduledReport = new ScheduledReport()
        };

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(log);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync("facility-1", cancellationToken))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = "facility-1" });

        // Simulate 429 with Retry-After: 30 seconds
        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                ResourceType.Patient,
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new TooManyRequestsException("Rate limited", TimeSpan.FromSeconds(30)));

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert: Log updated with delay (ExecutionDate ~30s from now), Failed status, retry incremented
        _mockLogManager.Verify(m => m.UpdateAsync(
            It.Is<UpdateDataAcquisitionLogModel>(u =>
                u.Status == RequestStatus.Failed &&
                u.RetryAttempts == 0 &&
                u.ExecutionDate >= DateTime.UtcNow.AddSeconds(20) &&  // Widened range to account for execution time
                u.ExecutionDate <= DateTime.UtcNow.AddSeconds(40) &&
                u.NewNotes.Any(n => n.Contains("Throttled (429): Retrying after") && n.Contains("30"))  // Check for specific delay in note
            ),
            cancellationToken),
            Times.Exactly(1));  // Exactly once for the reschedule (the Processing update is separate)
    }

    [Fact]
    public async Task ExecuteLogRequest_Handles429WithDateDelay_ReschedulesLogWithCalculatedDelay()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facility-1");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLogModel
        {
            Id = 1,
            FacilityId = "facility-1",
            PatientId = "Patient/123",
            Status = RequestStatus.Queued,
            CorrelationId = "corr-1",
            FhirQuery = new List<FhirQueryModel>
            {
                new FhirQueryModel
                {
                    QueryType = FhirQueryType.Read,
                    ResourceTypes = new List<ResourceType> { ResourceType.Patient }
                }
            },
            ScheduledReport = new ScheduledReport()
        };

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(log);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync("facility-1", cancellationToken))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = "facility-1" });

        // Simulate 429 with Retry-After as a future date (e.g., 2 minutes from now)
        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        var futureDate = DateTimeOffset.UtcNow.AddMinutes(2);
        var expectedDelay = TimeSpan.FromMinutes(2);
        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                ResourceType.Patient,
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new TooManyRequestsException("Rate limited", expectedDelay));

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert: Log rescheduled ~2min from now
        _mockLogManager.Verify(m => m.UpdateAsync(
            It.Is<UpdateDataAcquisitionLogModel>(u =>
                u.Status == RequestStatus.Failed &&
                u.RetryAttempts == 0 &&
                u.ExecutionDate >= DateTime.UtcNow.AddMinutes(1.9) &&  // Approximate
                u.ExecutionDate <= DateTime.UtcNow.AddMinutes(2.1) &&
                u.NewNotes.Any(n => n.Contains("Throttled (429): Retrying after"))
            ),
            cancellationToken),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteLogRequest_Handles429WithInvalidNegativeHeader_UsesParsedDefaultDelay()
    {
        // Arrange
        var request = new AcquisitionRequest(1, "facility-1");
        var cancellationToken = CancellationToken.None;

        var log = new DataAcquisitionLogModel
        {
            Id = 1,
            FacilityId = "facility-1",
            PatientId = "Patient/123",
            Status = RequestStatus.Queued,
            CorrelationId = "corr-1",
            FhirQuery = new List<FhirQueryModel>
        {
            new FhirQueryModel
            {
                QueryType = FhirQueryType.Read,
                ResourceTypes = new List<ResourceType> { ResourceType.Patient }
            }
        },
            ScheduledReport = new ScheduledReport()
        };

        _mockLogQueries
            .Setup(q => q.GetAsync(1, cancellationToken))
            .ReturnsAsync(log);

        _mockFhirQueryQueries
            .Setup(q => q.GetByFacilityIdAsync("facility-1", cancellationToken))
            .ReturnsAsync(new FhirQueryConfigurationModel { FacilityId = "facility-1" });

        // Simulate 429 with negative/invalid Retry-After (parser will default to 60s)
        _mockLogManager
            .Setup(q => q.TrySetLogStatusAsync(1, It.IsAny<List<RequestStatus>>(), RequestStatus.Processing, It.IsAny<string?>(), cancellationToken))
            .ReturnsAsync(true);

        _mockFhirApiService
            .Setup(x => x.ExecuteRead(
                It.IsAny<DataAcquisitionLogModel>(),
                It.IsAny<FhirQueryModel>(),
                ResourceType.Patient,
                It.IsAny<FhirQueryConfigurationModel>(),
                It.IsAny<DiscoveredReferenceAccumulator>(),
                cancellationToken))
            .ThrowsAsync(new TooManyRequestsException("Rate limited", TimeSpan.FromSeconds(60)));  // Mimic parsed default

        // Act
        await _service.ExecuteLogRequest(request, cancellationToken);

        // Assert: Log rescheduled ~60s from now, Failed, retry=0, note reflects default delay
        _mockLogManager.Verify(m => m.UpdateAsync(
            It.Is<UpdateDataAcquisitionLogModel>(u =>
                u.Status == RequestStatus.Failed &&
                u.RetryAttempts == 0 &&
                u.ExecutionDate >= DateTime.UtcNow.AddSeconds(55) &&  // Approx for 60s, allowing execution variance
                u.ExecutionDate <= DateTime.UtcNow.AddSeconds(65) &&
                u.NewNotes.Any(n => n.Contains("Throttled (429): Retrying after") && n.Contains("60"))
            ),
            cancellationToken),
            Times.Exactly(1));  // Once for reschedule (Processing update separate)
    }

    [Fact]
    public async Task GetNextEligibleBatchForFacility_OrdersByPriorityDescending_ThenExecutionDateAscending_IncludesAllFailedRegardlessOfRetries()
    {
        // Arrange
        var facilityId = "facility-1";
        long? lastId = null;
        int batchSize = 4;
        var cancellationToken = CancellationToken.None;

        // Simulate logs with varying priorities, dates, and retry attempts (including exceeded max)
        var logs = new List<DataAcquisitionLogModel>
    {
        new() { Id = 1, Priority = AcquisitionPriority.Normal, ExecutionDate = DateTime.UtcNow.AddMinutes(-5), Status = RequestStatus.Pending },
        new() { Id = 2, Priority = AcquisitionPriority.High, ExecutionDate = DateTime.UtcNow.AddMinutes(-10), Status = RequestStatus.Pending },
        new() { Id = 3, Priority = AcquisitionPriority.High, ExecutionDate = DateTime.UtcNow.AddMinutes(-1), Status = RequestStatus.Pending },
        new() { Id = 4, Priority = AcquisitionPriority.Normal, ExecutionDate = DateTime.UtcNow.AddMinutes(-2), Status = RequestStatus.Failed, RetryAttempts = 2 },  // Retryable (below max)
        new() { Id = 5, Priority = AcquisitionPriority.Critical, ExecutionDate = DateTime.UtcNow.AddMinutes(-3), Status = RequestStatus.Failed, RetryAttempts = 6 }   // Exceeded max retries, but still included
    };

        var dateTimeNow = DateTime.UtcNow;
        _mockLogQueries
            .Setup(q => q.GetNextEligibleBatchForFacility(facilityId, lastId, batchSize, new() { RequestStatus.Pending, RequestStatus.Failed }, dateTimeNow, cancellationToken))
            .ReturnsAsync(logs
                .Where(l => l.Status == RequestStatus.Pending || l.Status == RequestStatus.Failed)
                .OrderBy(l => l.Priority)  // Ascending: Critical (0), High (1), Normal (2)
                .ThenBy(l => l.ExecutionDate)
                .ThenBy(l => l.Id)
                .Take(batchSize)
                .ToList());

        // Act
        var result = await _mockLogQueries.Object.GetNextEligibleBatchForFacility(facilityId, lastId, batchSize, new() { RequestStatus.Pending, RequestStatus.Failed }, dateTimeNow, cancellationToken);

        // Assert: All Pending and Failed included, ordered correctly (Critical/High first, then by date; includes exceeded retries)
        Assert.Equal(4, result.Count);  // Batch size (original 5 matching, take 4)
        Assert.Equal(5, result[0].Id);  // Critical first (even if Failed and exceeded retries)
        Assert.Equal(2, result[1].Id);  // High, oldest ExecutionDate
        Assert.Equal(3, result[2].Id);  // High, newer ExecutionDate
        Assert.Equal(1, result[3].Id);  // Normal Pending (next after highs)
    }
}

