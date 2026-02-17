using System.Reflection;
using System.Text;
using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

public class ResourceAcquiredListenerTests
{
    private readonly Mock<ILogger<ResourceAcquiredListener>> _loggerMock;
    private readonly Mock<ServiceInformation> _serviceInformationMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IKafkaConsumerFactory<string, ResourceAcquiredMessage>> _consumerFactoryMock;
    private readonly Mock<IDeadLetterExceptionHandler<string, string>> _consumeExceptionHandlerMock;
    private readonly Mock<IDeadLetterExceptionHandler<string, ResourceAcquiredMessage>> _deadLetterExceptionHandlerMock;
    private readonly Mock<ITransientExceptionHandler<string, ResourceAcquiredMessage>> _transientExceptionHandlerMock;
    private readonly Mock<INormalizationServiceMetrics> _metricsMock;
    private readonly Mock<IProducer<string, ResourceNormalizedMessage>> _producerMock;
    private readonly Mock<CopyPropertyOperationService> _copyPropertyOperationServiceMock;
    private readonly Mock<CodeMapOperationService> _codeMapOperationServiceMock;
    private readonly Mock<ConditionalTransformOperationService> _conditionalTransformOperationServiceMock;
    private readonly Mock<CopyLocationOperationService> _copyLocationOperationServiceMock;

    public ResourceAcquiredListenerTests()
    {
        _loggerMock = new Mock<ILogger<ResourceAcquiredListener>>();
        _serviceInformationMock = new Mock<ServiceInformation>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _consumerFactoryMock = new Mock<IKafkaConsumerFactory<string, ResourceAcquiredMessage>>();
        _consumeExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<string, string>>();
        _deadLetterExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<string, ResourceAcquiredMessage>>();
        _transientExceptionHandlerMock = new Mock<ITransientExceptionHandler<string, ResourceAcquiredMessage>>();
        _metricsMock = new Mock<INormalizationServiceMetrics>();
        _producerMock = new Mock<IProducer<string, ResourceNormalizedMessage>>();
        
        // Mocking services that might not have parameterless constructors or are classes
        _copyPropertyOperationServiceMock = new Mock<CopyPropertyOperationService>(new Mock<ILogger<CopyPropertyOperationService>>().Object, null);
        _codeMapOperationServiceMock = new Mock<CodeMapOperationService>(new Mock<ILogger<CodeMapOperationService>>().Object, null);
        _conditionalTransformOperationServiceMock = new Mock<ConditionalTransformOperationService>(new Mock<ILogger<ConditionalTransformOperationService>>().Object, null);
        _copyLocationOperationServiceMock = new Mock<CopyLocationOperationService>(new Mock<ILogger<CopyLocationOperationService>>().Object, null);
    }

    [Fact]
    public async Task ProduceResourceNormalizedMessage_ShouldThrowTransientException_WhenProduceExceptionOccurs()
    {
        // Arrange
        var listener = new ResourceAcquiredListener(
            _loggerMock.Object,
            _serviceInformationMock.Object,
            _scopeFactoryMock.Object,
            _consumerFactoryMock.Object,
            _consumeExceptionHandlerMock.Object,
            _deadLetterExceptionHandlerMock.Object,
            _transientExceptionHandlerMock.Object,
            _metricsMock.Object,
            _producerMock.Object,
            _copyPropertyOperationServiceMock.Object,
            _codeMapOperationServiceMock.Object,
            _conditionalTransformOperationServiceMock.Object,
            _copyLocationOperationServiceMock.Object);

        var facilityId = "TestFacility";
        var correlationId = "TestCorrelationId";
        var resource = new Patient { Id = "TestPatient" };
        var messageValue = new ResourceAcquiredMessage
        {
            PatientId = "TestPatient",
            QueryType = "TestQuery",
            ScheduledReports = new List<ScheduledReport> { new ScheduledReport { ReportTypes = new List<string> { "TestReport" } } },
            ReportableEvent = "TestEvent",
            AcquisitionComplete = false
        };

        var consumeResult = new ConsumeResult<string, ResourceAcquiredMessage>
        {
            Message = new Message<string, ResourceAcquiredMessage>
            {
                Key = facilityId,
                Value = messageValue
            }
        };

        var produceException = new ProduceException<string, ResourceNormalizedMessage>(
            new Error(ErrorCode.Local_Application, "Test Kafka Error"),
            new DeliveryResult<string, ResourceNormalizedMessage>());

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, ResourceNormalizedMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(produceException);

        // Act & Assert
        var methodInfo = typeof(ResourceAcquiredListener).GetMethod("ProduceResourceNormalizedMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(methodInfo);

        var task = (Task)methodInfo.Invoke(listener, new object[] { consumeResult, facilityId, correlationId, resource });
        
        var exception = await Assert.ThrowsAsync<TransientException>(() => task);
        Assert.Contains("Failed to produce ResourceNormalized message", exception.Message);
        Assert.Same(produceException, exception.InnerException);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to produce ResourceNormalized message")),
                produceException,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
