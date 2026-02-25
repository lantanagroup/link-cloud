using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

public class ResourceAcquiredListenerTests
{
    private readonly Mock<ILogger<ResourceAcquiredListener>> _loggerMock;
    private readonly Mock<ServiceInformation> _serviceInformationMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IKafkaConsumerFactory<ResourceKey, ResourceAcquiredMessage>> _consumerFactoryMock;
    private readonly Mock<IDeadLetterExceptionHandler<ResourceKey, string>> _consumeExceptionHandlerMock;
    private readonly Mock<IDeadLetterExceptionHandler<ResourceKey, ResourceAcquiredMessage>> _deadLetterExceptionHandlerMock;
    private readonly Mock<ITransientExceptionHandler<ResourceKey, ResourceAcquiredMessage>> _transientExceptionHandlerMock;
    private readonly Mock<INormalizationServiceMetrics> _metricsMock;
    private readonly Mock<IProducer<ResourceKey, ResourceNormalizedMessage>> _producerMock;
    private readonly Mock<CopyPropertyOperationService> _copyPropertyOperationServiceMock;
    private readonly Mock<CodeMapOperationService> _codeMapOperationServiceMock;
    private readonly Mock<ConditionalTransformOperationService> _conditionalTransformOperationServiceMock;
    private readonly Mock<CopyLocationOperationService> _copyLocationOperationServiceMock;

    public ResourceAcquiredListenerTests()
    {
        _loggerMock = new Mock<ILogger<ResourceAcquiredListener>>();
        _serviceInformationMock = new Mock<ServiceInformation>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _consumerFactoryMock = new Mock<IKafkaConsumerFactory<ResourceKey, ResourceAcquiredMessage>>();
        _consumeExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<ResourceKey, string>>();
        _deadLetterExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<ResourceKey, ResourceAcquiredMessage>>();
        _transientExceptionHandlerMock = new Mock<ITransientExceptionHandler<ResourceKey, ResourceAcquiredMessage>>();
        _metricsMock = new Mock<INormalizationServiceMetrics>();
        _producerMock = new Mock<IProducer<ResourceKey, ResourceNormalizedMessage>>();
        
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

        var consumeResult = new ConsumeResult<ResourceKey, ResourceAcquiredMessage>
        {
            Message = new Message<ResourceKey, ResourceAcquiredMessage>
            {
                Key = new ResourceKey { FacilityId = facilityId, CorrelationId = correlationId },
                Value = messageValue
            }
        };

        var produceException = new ProduceException<ResourceKey, ResourceNormalizedMessage>(
            new Error(ErrorCode.Local_Application, "Test Kafka Error"),
            new DeliveryResult<ResourceKey, ResourceNormalizedMessage>());

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourceNormalizedMessage>>(), It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task ProduceResourceNormalizedMessage_ShouldThrowTransientException_WhenProduceExceptionOccursAndResourceIsNull()
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
        DomainResource resource = null; // This is the case we want to test
        var messageValueNull = new ResourceAcquiredMessage
        {
            PatientId = "TestPatient",
            QueryType = "TestQuery",
            ScheduledReports = new List<ScheduledReport> { new ScheduledReport { ReportTypes = new List<string> { "TestReport" } } },
            ReportableEvent = "TestEvent",
            AcquisitionComplete = true // Typical for null resource
        };

        var consumeResultNull = new ConsumeResult<ResourceKey, ResourceAcquiredMessage>
        {
            Message = new Message<ResourceKey, ResourceAcquiredMessage>
            {
                Key = new ResourceKey { FacilityId = facilityId, CorrelationId = correlationId },
                Value = messageValueNull
            }
        };

        var produceExceptionNull = new ProduceException<ResourceKey, ResourceNormalizedMessage>(
            new Error(ErrorCode.Local_Application, "Test Kafka Error"),
            new DeliveryResult<ResourceKey, ResourceNormalizedMessage>());

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourceNormalizedMessage>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(produceExceptionNull);

        // Act & Assert
        var methodInfo = typeof(ResourceAcquiredListener).GetMethod("ProduceResourceNormalizedMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(methodInfo);

        var task = (Task)methodInfo.Invoke(listener, new object[] { consumeResultNull, facilityId, correlationId, resource });
        
        var exception = await Assert.ThrowsAsync<TransientException>(() => task);
        Assert.Contains("Failed to produce ResourceNormalized message", exception.Message);
        Assert.Same(produceExceptionNull, exception.InnerException);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to produce ResourceNormalized message")),
                produceExceptionNull,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
