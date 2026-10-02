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
using LantanaGroup.Link.Shared.Application.Models.Mapping;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using FhirResourceType = Hl7.Fhir.Model.ResourceType;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// After ResourcesNormalized is produced, the acquisition keys are released. If that release fails
/// partway, or the pod dies before the offset commit, the message comes back with some of its listed
/// keys already gone. Dead-lettering it then purged {correlationId}, which MeasureEval had just been
/// told to read.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredListenerRedeliveryTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    private static readonly string PatientCacheKey = $"{CorrelationId}:Patient";
    private static readonly string EncounterCacheKey = $"{CorrelationId}:Encounter";

    [Fact]
    public async Task ProcessMessageAsync_DeleteAfterProduceFails_CompletesWithoutThrowing()
    {
        var resourceCache = PopulatedCache(PatientCacheKey, FhirResourceType.Patient, new Patient { Id = PatientId });
        resourceCache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("blob delete failed"));

        var producer = ProducingProducer();
        var listener = BuildListener(resourceCache, producer);

        // A throw here sent the message to retry, and the retry found the deleted key empty.
        await listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey]), CancellationToken.None);

        producer.Verify(
            item => item.ProduceAsync(
                KafkaTopic.ResourcesNormalized.ToString(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_Barrier_WaitsOnlyOnTheResourcesThisMessageAppended()
    {
        // A failure on the correlation key is kept until its resources are written again, and that
        // rewrite may happen on another pod. Waiting on the whole key here would keep failing on a
        // failure this pod still remembers, for data durable storage already holds.
        var resourceCache = PopulatedCache(PatientCacheKey, FhirResourceType.Patient, new Patient { Id = PatientId });
        IReadOnlyCollection<string>? waitedFor = null;
        resourceCache
            .Setup(item => item.WaitForDurableAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, IReadOnlyCollection<string>, CancellationToken>((_, references, _) => waitedFor = references)
            .Returns(Task.CompletedTask);

        var listener = BuildListener(resourceCache, ProducingProducer());

        await listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey]), CancellationToken.None);

        Assert.Equal(new[] { $"Patient/{PatientId}" }, waitedFor);
        resourceCache.Verify(
            item => item.WaitForDurableAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        resourceCache.Verify(
            item => item.WaitForDurableAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_DeleteAfterProduceCancelled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        var resourceCache = PopulatedCache(PatientCacheKey, FhirResourceType.Patient, new Patient { Id = PatientId });
        resourceCache
            .Setup(item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });

        var listener = BuildListener(resourceCache, ProducingProducer());

        // Shutdown is not a cleanup failure to swallow: the offset must not be committed.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey]), cancellation.Token));
    }

    [Fact]
    public async Task ProcessMessageAsync_FirstListedKeyEmptyAndCorrelationEntryPresent_AcknowledgesWithoutProducing()
    {
        // The release deletes keys in listed order and got past the Patient key before failing or the
        // pod died: Patient is gone, Encounter is still there, and the correlation entry holds the
        // normalized output.
        var resourceCache = PopulatedCache(EncounterCacheKey, FhirResourceType.Encounter, new Encounter { Id = "encounter-1" });
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(PatientCacheKey))
            .Returns(FhirResourceType.Patient);
        resourceCache
            .Setup(item => item.GetAsync(PatientCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        resourceCache
            .Setup(item => item.HasResourcesAsync(CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var producer = ProducingProducer();
        var listener = BuildListener(resourceCache, producer);

        await listener.ProcessMessageAsync(
            BuildConsumeResult([PatientCacheKey, EncounterCacheKey]),
            CancellationToken.None);

        // Already produced the first time round; producing again would evaluate the patient twice.
        producer.Verify(
            item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Nothing is copied again, and the release is finished.
        resourceCache.Verify(
            item => item.AppendResourcesAsync(
                It.IsAny<string>(),
                It.IsAny<List<DomainResource>>(),
                It.IsAny<FhirResourceType>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        resourceCache.Verify(
            item => item.DeleteAsync(
                It.Is<List<string>>(keys => keys.SequenceEqual(new[] { PatientCacheKey, EncounterCacheKey })),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Never the correlation entry.
        resourceCache.Verify(
            item => item.DeleteAsync(
                It.Is<List<string>>(keys => keys.Contains(CorrelationId)),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_EmptyKeyAfterAPopulatedOne_DeadLetters()
    {
        // A release deletes in listed order, so it can never leave an empty key after a populated one.
        // This shape is Data Acquisition listing a key it never wrote. The correlation entry is
        // populated only because this pass just appended Patient to it, which must not be read as
        // proof the message was already normalized.
        var resourceCache = PopulatedCache(PatientCacheKey, FhirResourceType.Patient, new Patient { Id = PatientId });
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(EncounterCacheKey))
            .Returns(FhirResourceType.Encounter);
        resourceCache
            .Setup(item => item.GetAsync(EncounterCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        resourceCache
            .Setup(item => item.HasResourcesAsync(CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var producer = ProducingProducer();
        var listener = BuildListener(resourceCache, producer);

        var thrown = await Assert.ThrowsAsync<DeadLetterException>(() =>
            listener.ProcessMessageAsync(BuildConsumeResult([PatientCacheKey, EncounterCacheKey]), CancellationToken.None));

        Assert.Contains(EncounterCacheKey, thrown.Message);
        producer.Verify(
            item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        resourceCache.Verify(
            item => item.DeleteAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeMessageAsync_RedeliveryAfterPartialDelete_NeverPurges()
    {
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(PatientCacheKey))
            .Returns(FhirResourceType.Patient);
        resourceCache
            .Setup(item => item.GetAsync(PatientCacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        resourceCache
            .Setup(item => item.HasResourcesAsync(CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var purger = new Mock<IResourceCachePurger>();
        var deadLetterHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        var listener = BuildListener(resourceCache, ProducingProducer(), purger.Object, deadLetterHandler);

        await listener.ConsumeMessageAsync(BuildConsumeResult([PatientCacheKey]), CancellationToken.None);

        purger.VerifyNoOtherCalls();
        deadLetterHandler.Verify(
            item => item.HandleException(
                It.IsAny<ConsumeResult<ResourceKey, ResourcesAcquiredValue>>(),
                It.IsAny<DeadLetterException>(),
                It.IsAny<string>()),
            Times.Never);
    }

    private static Mock<IResourceCache> PopulatedCache(string cacheKey, FhirResourceType resourceType, DomainResource resource)
    {
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetResourceTypeByCacheKey(cacheKey))
            .Returns(resourceType);
        resourceCache
            .Setup(item => item.GetAsync(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync([resource]);

        return resourceCache;
    }

    private static Mock<IProducer<ResourceKey, ResourcesNormalizedValue>> ProducingProducer()
    {
        var producer = new Mock<IProducer<ResourceKey, ResourcesNormalizedValue>>();
        producer
            .Setup(item => item.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ResourceKey, ResourcesNormalizedValue>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<ResourceKey, ResourcesNormalizedValue>());

        return producer;
    }

    private static ResourcesAcquiredListener BuildListener(
        Mock<IResourceCache> resourceCache,
        Mock<IProducer<ResourceKey, ResourcesNormalizedValue>> producer,
        IResourceCachePurger? purger = null,
        Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>? deadLetterHandler = null)
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

        deadLetterHandler ??= new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
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
            new HSLOCMapOperationService(Mock.Of<ILogger<HSLOCMapOperationService>>(),
                new CodeMapOperationService(Mock.Of<ILogger<CodeMapOperationService>>())),
            new ConditionalTransformOperationService(Mock.Of<ILogger<ConditionalTransformOperationService>>()),
            new CopyLocationOperationService(Mock.Of<ILogger<CopyLocationOperationService>>()),
            new CopyLocationAliasToTypeIterativelyOperationService(Mock.Of<ILogger<CopyLocationAliasToTypeIterativelyOperationService>>()),
            new RemoveExtensionsOperationService(Mock.Of<ILogger<RemoveExtensionsOperationService>>()),
            resourceCache.Object,
            purger ?? Mock.Of<IResourceCachePurger>(),
            telemetrySettings.Object,
            Mock.Of<IProducer<ResourceKey, MappingOutcomeEvaluatedValue>>());
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
