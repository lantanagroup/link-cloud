using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Queries;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services.FhirApi.Commands;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
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
        private readonly Mock<IDataAcquisitionLogQueries> _dataAcquisitionLogQueries;

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
            _dataAcquisitionLogQueries = new Mock<IDataAcquisitionLogQueries>();

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
                _dataAcquisitionLogQueries.Object
            );
        }

        [Fact]
        public async Task ValidateFacilityConnection_ShouldReturnResources_WhenValidRequest()
        {
            // Arrange
            var request = new GetPatientDataRequest();
            var cancellationToken = CancellationToken.None;

            _mockReadFhirCommand
                .Setup(cmd => cmd.ExecuteAsync(It.IsAny<ReadFhirCommandRequest>(), cancellationToken))
                .ReturnsAsync(new Patient());

            // Act
            var result = await _service.ValidateFacilityConnection(request, cancellationToken);

            // Assert
            Assert.NotNull(result);
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
            var request = new GetPatientDataRequest();
            var cancellationToken = CancellationToken.None;

            _mockLogManager
                .Setup(manager => manager.CreateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken))
                .ReturnsAsync(new DataAcquisitionLog());

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

            _mockLogManager
                .Setup(manager => manager.CreateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken))
                .ReturnsAsync(new DataAcquisitionLog());

            // Act
            await _service.ExecuteLogRequest(request, cancellationToken);

            // Assert
            _mockLogManager.Verify(manager => manager.CreateAsync(It.IsAny<DataAcquisitionLog>(), cancellationToken), Times.Once);
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
