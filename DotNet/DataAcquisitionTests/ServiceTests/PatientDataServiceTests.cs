using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.QueryConfig;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Linq.Expressions;
using Xunit;
using RequestStatus = LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums.RequestStatus;
using ResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.DataAcquisitionTests.ServiceTests
{
    public class PatientDataServiceTests
    {
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<PatientDataService>> _mockLogger;
        private readonly Mock<IFhirQueryConfigurationManager> _mockFhirQueryManager;
        private readonly Mock<IQueryPlanManager> _mockQueryPlanManager;
        private readonly Mock<IProducer<string, ResourceAcquired>> _mockKafkaProducer;
        private readonly Mock<IQueryListProcessor> _mockQueryListProcessor;
        private readonly Mock<IReadFhirCommand> _mockReadFhirCommand;
        private readonly Mock<ISearchFhirCommand> _mockSearchFhirCommand;
        private readonly Mock<IDataAcquisitionLogManager> _mockLogManager;
        private readonly Mock<IReferenceResourcesManager> _mockReferenceResourcesManager;
        private readonly Mock<IDataAcquisitionLogQueries> _mockLogQueries;
        private readonly Mock<IReferenceResourceService> _mockRefService;

        private readonly PatientDataService _service;

        public PatientDataServiceTests()
        {
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<PatientDataService>>();
            _mockFhirQueryManager = new Mock<IFhirQueryConfigurationManager>();
            _mockQueryPlanManager = new Mock<IQueryPlanManager>();
            _mockKafkaProducer = new Mock<IProducer<string, ResourceAcquired>>();
            _mockQueryListProcessor = new Mock<IQueryListProcessor>();
            _mockReadFhirCommand = new Mock<IReadFhirCommand>();
            _mockSearchFhirCommand = new Mock<ISearchFhirCommand>();
            _mockLogManager = new Mock<IDataAcquisitionLogManager>();
            _mockReferenceResourcesManager = new Mock<IReferenceResourcesManager>();
            _mockLogQueries = new Mock<IDataAcquisitionLogQueries>();
            _mockRefService = new Mock<IReferenceResourceService>();

            _service = new PatientDataService(
                _mockDatabase.Object,
                _mockLogger.Object,
                _mockFhirQueryManager.Object,
                _mockQueryPlanManager.Object,
                _mockKafkaProducer.Object,
                _mockQueryListProcessor.Object,
                _mockReadFhirCommand.Object,
                _mockSearchFhirCommand.Object,
                _mockLogManager.Object,
                _mockReferenceResourcesManager.Object,
                _mockLogQueries.Object,
                _mockRefService.Object
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

            var fhirQueryConfig = new FhirQueryConfiguration
            {
                FacilityId = "facility-1",
                FhirServerBaseUrl = "http://example.com",
                TimeZone = "UTC"
            };

            var queryPlan = new QueryPlan
            {
                FacilityId = "facility-1",
                Type = Frequency.Discharge,
                InitialQueries = new Dictionary<string, IQueryConfig>
                {
                    { "q1", new ReferenceQueryConfig { ResourceType = ResourceType.Patient.ToString() } }
                },
                SupplementalQueries = new Dictionary<string, IQueryConfig>()
            };

            _mockFhirQueryManager
                .Setup(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fhirQueryConfig);

            _mockQueryPlanManager
                .Setup(m => m.FindAsync(It.IsAny<Expression<Func<QueryPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<QueryPlan> { queryPlan });

            _mockReadFhirCommand
                .Setup(cmd => cmd.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), cancellationToken))
                .ReturnsAsync(new Patient());

            _mockQueryListProcessor
                .Setup(p => p.ExecuteFacilityValidationRequest(
                    It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                    It.IsAny<GetPatientDataRequest>(),
                    It.IsAny<FhirQueryConfiguration>(),
                    It.IsAny<ScheduledReport>(), // Corrected argument type
                    It.IsAny<QueryPlan>(),
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

            var fhirQueryConfig = new FhirQueryConfiguration
            {
                FacilityId = "facility-1",
                FhirServerBaseUrl = "http://example.com",
                TimeZone = "UTC"
            };

            var queryPlan = new QueryPlan
            {
                FacilityId = "facility-1",
                Type = Frequency.Discharge,
                InitialQueries = new Dictionary<string, IQueryConfig>
                {
                    { "q1", new ReferenceQueryConfig { ResourceType = ResourceType.Patient.ToString() } }
                },
                SupplementalQueries = new Dictionary<string, IQueryConfig>()
            };

            _mockFhirQueryManager
                .Setup(m => m.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(fhirQueryConfig);

            _mockQueryPlanManager
                .Setup(m => m.FindAsync(It.IsAny<Expression<Func<QueryPlan, bool>>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<QueryPlan> { queryPlan });

            _mockLogManager
                .Setup(manager => manager.CreateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken))
                .ReturnsAsync(new DataAcquisitionLog());

            _mockQueryListProcessor
                .Setup(p => p.Process(
                    It.IsAny<IOrderedEnumerable<KeyValuePair<string, IQueryConfig>>>(),
                    It.IsAny<GetPatientDataRequest>(),
                    It.IsAny<FhirQueryConfiguration>(),
                    It.IsAny<QueryPlan>(),
                    It.IsAny<List<ResourceReferenceType>>(),
                    It.IsAny<string>(),
                    It.IsAny<ScheduledReport>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _service.CreateLogEntries(request, cancellationToken);

            // Assert
            _mockLogManager.Verify(manager => manager.CreateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken), Times.Once);
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
            var request = new AcquisitionRequest("logId", "facilityId");
            var cancellationToken = CancellationToken.None;

            var log = new DataAcquisitionLog
            {
                Id = "logId",
                FacilityId = "facilityId",
                Status = RequestStatus.Ready,
                FhirQuery = new List<FhirQuery>
                {
                    new FhirQuery
                    {
                        QueryType = FhirQueryType.Read,
                        ResourceTypes = new List<ResourceType> { ResourceType.Patient },
                        QueryParameters = new List<string>(),
                        ResourceReferenceTypes = new List<ResourceReferenceType>()
                    }
                },
                ScheduledReport = new ScheduledReport(),
                PatientId = "patient-1",
                CorrelationId = "corr-1"
            };

            var fhirQueryConfig = new FhirQueryConfiguration
            {
                FacilityId = "facilityId",
                FhirServerBaseUrl = "http://example.com"
            };

            _mockLogQueries
                .Setup(q => q.GetCompleteLogAsync("logId", cancellationToken))
                .ReturnsAsync(log);

            _mockLogManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken))
                .ReturnsAsync(log);

            _mockFhirQueryManager
                .Setup(m => m.GetAsync("facilityId", cancellationToken))
                .ReturnsAsync(fhirQueryConfig);

            _mockReadFhirCommand
                .Setup(cmd => cmd.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), cancellationToken))
                .ReturnsAsync(new Patient { Id = "patient-1" });

            // Act
            await _service.ExecuteLogRequest(request, cancellationToken);

            // Assert
            _mockLogManager.Verify(manager => manager.UpdateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken), Times.AtLeastOnce);
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
    }
}
