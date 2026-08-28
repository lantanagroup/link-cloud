using Confluent.Kafka;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Application.Models.Operations.Business.Query;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Domain.Queries;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text;
using FhirResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// Data Acquisition lists a cache key only when that key currently has resources.
/// An empty listed key is a producer defect and is dead-lettered (not retried).
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredListenerEmptyCacheTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    private static readonly string PatientCacheKey = $"{CorrelationId}:Patient";
    private static readonly string EncounterCacheKey = $"{CorrelationId}:Encounter";

    [Fact]
    public async Task ProcessMessageAsync_ListedCacheKeyEmpty_ThrowsDeadLetterAndDoesNotDeleteOrProduce()
    {
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(ResourceCacheType.ABS))
            .Returns(resourceCache.Object);
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(PatientCacheKey))
            .Returns(FhirResourceType.Patient);
        resourceCache
            .Setup(item => item.GetAsync(PatientCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var producer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        var listener = BuildListener(resourceCache, producer);

        var ex = await Assert.ThrowsAsync<DeadLetterException>(() =>
            listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey]), CancellationToken.None));

        Assert.Contains("contained no resources", ex.Message);
        Assert.Contains(PatientCacheKey, ex.Message);
        resourceCache.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        resourceCache.Verify(
            item => item.UpdateCorrelationCacheAsync(
                It.IsAny<string>(),
                It.IsAny<List<DomainResource>>(),
                It.IsAny<FhirResourceType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        producer.Verify(
            item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ListedCacheKeyPopulated_CopiesThenDeletesSourceKey()
    {
        var patient = new Patient { Id = "patient-1" };
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(ResourceCacheType.ABS))
            .Returns(resourceCache.Object);
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(PatientCacheKey))
            .Returns(FhirResourceType.Patient);
        resourceCache
            .Setup(item => item.GetAsync(PatientCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([patient]);
        resourceCache
            .Setup(item => item.UpdateCorrelationCacheAsync(
                CorrelationId,
                It.IsAny<List<DomainResource>>(),
                FhirResourceType.Patient,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        resourceCache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var producer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        producer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourcesNormalizedValue>());

        var listener = BuildListener(resourceCache, producer);

        await listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey]), CancellationToken.None);

        resourceCache.Verify(
            item => item.UpdateCorrelationCacheAsync(
                CorrelationId,
                It.Is<List<DomainResource>>(resources => resources.Count == 1),
                FhirResourceType.Patient,
                It.IsAny<CancellationToken>()),
            Times.Once);
        resourceCache.Verify(
            item => item.DeleteAsync(
                It.Is<List<string>>(keys => keys.Count == 1 && keys[0] == PatientCacheKey),
                It.IsAny<CancellationToken>()),
            Times.Once);
        producer.Verify(
            item => item.ProduceAsync(
                KafkaTopic.ResourcesNormalized.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_MixedKeys_ThrowsDeadLetterAndDoesNotProduce()
    {
        var patient = new Patient { Id = "patient-1" };
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(ResourceCacheType.ABS))
            .Returns(resourceCache.Object);
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(PatientCacheKey))
            .Returns(FhirResourceType.Patient);
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(EncounterCacheKey))
            .Returns(FhirResourceType.Encounter);
        resourceCache
            .Setup(item => item.GetAsync(PatientCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([patient]);
        resourceCache
            .Setup(item => item.GetAsync(EncounterCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var producer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        var listener = BuildListener(resourceCache, producer);

        var ex = await Assert.ThrowsAsync<DeadLetterException>(() =>
            listener.ProcessMessageAsync(
                BuildConsumeResult([PatientCacheKey, EncounterCacheKey]),
                CancellationToken.None));

        Assert.Contains(EncounterCacheKey, ex.Message);
        resourceCache.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        producer.Verify(
            item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_NoCacheKeys_ProducesWithoutRetry()
    {
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(ResourceCacheType.ABS))
            .Returns(resourceCache.Object);
        resourceCache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var producer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        producer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourcesNormalizedValue>());

        var listener = BuildListener(resourceCache, producer);

        await listener.ProcessMessageAsync(BuildConsumeResult([]), CancellationToken.None);

        resourceCache.Verify(
            item => item.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        producer.Verify(
            item => item.ProduceAsync(
                KafkaTopic.ResourcesNormalized.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ResourcesAcquiredListener BuildListener(
        Mock<IResourceCache> resourceCache,
        Mock<IProducer<ResourceKey, ResourcesNormalizedValue>> producer)
    {
        var sequenceQueries = new Mock<IOperationSequenceQueries>();
        sequenceQueries
            .Setup(item => item.Search(
                It.IsAny<OperationSequenceSearchModel>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OperationSequenceModel>());

        var services = new ServiceCollection();
        services.AddSingleton(sequenceQueries.Object);
        var serviceProvider = services.BuildServiceProvider();

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(item => item.ServiceProvider).Returns(serviceProvider);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(item => item.CreateScope()).Returns(scope.Object);

        var deadLetterHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        deadLetterHandler.SetupProperty(item => item.Topic);
        var transientHandler = new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        transientHandler.SetupProperty(item => item.Topic);
        var consumeExceptionHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>>();
        consumeExceptionHandler.SetupProperty(item => item.Topic);

        var telemetrySettings = new Mock<IOptionsMonitor<TelemetrySettings>>();
        telemetrySettings.SetupGet(x => x.CurrentValue).Returns(new TelemetrySettings { PatientTags = false });

        return new ResourcesAcquiredListener(
            Mock.Of<ILogger<ResourcesAcquiredListener>>(),
            new ServiceInformation { ServiceConfigName = "Normalization" },
            scopeFactory.Object,
            Mock.Of<IKafkaConsumerFactory<ResourceKey, ResourcesAcquiredValue>>(),
            consumeExceptionHandler.Object,
            deadLetterHandler.Object,
            transientHandler.Object,
            Mock.Of<INormalizationServiceMetrics>(),
            producer.Object,
            new CopyPropertyOperationService(Mock.Of<ILogger<CopyPropertyOperationService>>()),
            new CodeMapOperationService(Mock.Of<ILogger<CodeMapOperationService>>()),
            new ConditionalTransformOperationService(Mock.Of<ILogger<ConditionalTransformOperationService>>()),
            new CopyLocationOperationService(Mock.Of<ILogger<CopyLocationOperationService>>()),
            new CopyLocationAliasToTypeIterativelyOperationService(Mock.Of<ILogger<CopyLocationAliasToTypeIterativelyOperationService>>()),
            new RemoveExtensionsOperationService(Mock.Of<ILogger<RemoveExtensionsOperationService>>()),
            resourceCache.Object,
            Mock.Of<IResourceCachePurger>(),
            telemetrySettings.Object);
    }

    private static ConsumeResult<ResourceKey, ResourcesAcquiredValue> BuildConsumeResult(List<string> cacheKeys)
    {
        var headers = new Headers
        {
            new Header(NormalizationConstants.HeaderNames.CorrelationId, Encoding.UTF8.GetBytes(CorrelationId))
        };

        return new ConsumeResult<ResourceKey, ResourcesAcquiredValue>
        {
            Topic = "ResourcesAcquired",
            Partition = new Partition(0),
            Offset = new Offset(0),
            Message = new Message<ResourceKey, ResourcesAcquiredValue>
            {
                Headers = headers,
                Key = new ResourceKey { FacilityId = FacilityId, PatientId = PatientId },
                Value = new ResourcesAcquiredValue
                {
                    QueryType = "Initial",
                    ReportableEvent = "Adhoc",
                    ScheduledReports = new List<ScheduledReport> { new() { ReportTrackingId = "tracking-1" } },
                    CacheType = ResourceCacheType.ABS,
                    CacheKeys = cacheKeys
                }
            }
        };
    }
}
