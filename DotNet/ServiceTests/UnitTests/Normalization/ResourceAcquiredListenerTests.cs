using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.ResourceCache;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class ResourceAcquiredListenerTests
{
    private readonly Mock<ILogger<ResourcesAcquiredListener>> _loggerMock;
    private readonly Mock<ServiceInformation> _serviceInformationMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>> _consumerFactoryMock;
    private readonly Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>> _consumeExceptionHandlerMock;
    private readonly Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>> _deadLetterExceptionHandlerMock;
    private readonly Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>> _transientExceptionHandlerMock;
    private readonly Mock<INormalizationServiceMetrics> _metricsMock;
    private readonly Mock<IProducer<ResourceKey, ResourcesNormalizedValue>> _producerMock;
    private readonly Mock<CopyPropertyOperationService> _copyPropertyOperationServiceMock;
    private readonly Mock<CodeMapOperationService> _codeMapOperationServiceMock;
    private readonly Mock<ConditionalTransformOperationService> _conditionalTransformOperationServiceMock;
    private readonly Mock<CopyLocationOperationService> _copyLocationOperationServiceMock;
    private readonly Mock<RedisResourceCache> _redisResourceCache;
    private readonly Mock<ABSResourceCache> _absResourceCache;

    public ResourceAcquiredListenerTests()
    {
        _loggerMock = new Mock<ILogger<ResourcesAcquiredListener>>();
        _serviceInformationMock = new Mock<ServiceInformation>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _consumerFactoryMock = new Mock<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>>();
        _consumeExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>>();
        _deadLetterExceptionHandlerMock = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        _transientExceptionHandlerMock = new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        _metricsMock = new Mock<INormalizationServiceMetrics>();
        _producerMock = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();

        // Mocking services that might not have parameterless constructors or are classes
        _copyPropertyOperationServiceMock = new Mock<CopyPropertyOperationService>(new Mock<ILogger<CopyPropertyOperationService>>().Object, null);
        _codeMapOperationServiceMock = new Mock<CodeMapOperationService>(new Mock<ILogger<CodeMapOperationService>>().Object, null);
        _conditionalTransformOperationServiceMock = new Mock<ConditionalTransformOperationService>(new Mock<ILogger<ConditionalTransformOperationService>>().Object, null);
        _copyLocationOperationServiceMock = new Mock<CopyLocationOperationService>(new Mock<ILogger<CopyLocationOperationService>>().Object, null);

        _redisResourceCache = new Mock<RedisResourceCache>();
        _absResourceCache = new Mock<ABSResourceCache>();
    }

    [Fact]
    public async Task ProduceResourceNormalizedMessage_ShouldThrowTransientException_WhenProduceExceptionOccurs()
    {
        // Arrange
        var listener = new ResourcesAcquiredListener(
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
            _copyLocationOperationServiceMock.Object,
            _redisResourceCache.Object,
            _absResourceCache.Object);

        var facilityId = "TestFacility";
        var correlationId = "TestCorrelationId";
        var resource = new Patient { Id = "TestPatient" };
        var messageValue = new ResourcesAcquiredValue
        {
            QueryType = "TestQuery",
            ScheduledReports = new List<ScheduledReport> { new ScheduledReport { ReportTypes = new List<string> { "TestReport" } } },
            ReportableEvent = "TestEvent",
            CacheType = ResourceCacheType.ABS,
            CacheKeys = new List<string>() { correlationId + ":" + ResourceType.Patient.ToString() }
        };

        var consumeResult = new ConsumeResult<ResourceKey, ResourcesAcquiredValue>
        {
            Message = new Message<ResourceKey, ResourcesAcquiredValue>
            {
                Key = new ResourceKey { FacilityId = facilityId, PatientId = "TestPatient"},
                Value = messageValue
            }
        };

        var produceException = new ProduceException<ResourceKey, ResourcesNormalizedValue>(
            new Error(ErrorCode.Local_Application, "Test Kafka Error"),
            new DeliveryResult<ResourceKey, ResourcesNormalizedValue>());

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(produceException);

        // Act & Assert
        var methodInfo = typeof(ResourcesAcquiredListener).GetMethod("ProduceResourceNormalizedMessage", BindingFlags.NonPublic | BindingFlags.Instance);
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
        var listener = new ResourcesAcquiredListener(
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
            _copyLocationOperationServiceMock.Object,
            _redisResourceCache.Object,
            _absResourceCache.Object);

        var facilityId = "TestFacility";
        var correlationId = "TestCorrelationId";
        DomainResource resource = null; // This is the case we want to test
        
        var messageValueNull = new ResourcesAcquiredValue
        {
            QueryType = "TestQuery",
            ScheduledReports = new List<ScheduledReport> { new ScheduledReport { ReportTypes = new List<string> { "TestReport" } } },
            ReportableEvent = "TestEvent",
            CacheType = ResourceCacheType.Redis,
            CacheKeys = new List<string>() { correlationId + ":" + ResourceType.Patient.ToString() }
        };

        var consumeResultNull = new ConsumeResult<ResourceKey, ResourcesAcquiredValue>
        {
            Message = new Message<ResourceKey, ResourcesAcquiredValue>
            {
                Key = new ResourceKey { FacilityId = facilityId, PatientId = "TestPatient"},
                Value = messageValueNull
            }
        };

        var produceExceptionNull = new ProduceException<ResourceKey, ResourcesNormalizedValue>(
            new Error(ErrorCode.Local_Application, "Test Kafka Error"),
            new DeliveryResult<ResourceKey, ResourcesNormalizedValue>());

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(produceExceptionNull);

        // Act & Assert
        var methodInfo = typeof(ResourcesAcquiredListener).GetMethod("ProduceResourceNormalizedMessage", BindingFlags.NonPublic | BindingFlags.Instance);
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
