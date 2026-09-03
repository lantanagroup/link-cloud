using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Normalization.Application.Services.Operations;
using LantanaGroup.Link.Normalization.Application.Settings;
using LantanaGroup.Link.Normalization.Listeners;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// The resource cache may only be released on terminal failures. A message bound for
/// ResourcesAcquired-Retry still needs its cached resources when it is redelivered.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredListenerCacheCleanupTests
{
    private const string FacilityId = "facility-1";
    private const string PatientId = "patient-1";
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    [Fact]
    public async Task ConsumeMessageAsync_DeadLetterFailure_PurgesTheResourceCache()
    {
        var purger = new Mock<IResourceCachePurger>();
        var deadLetterHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        var listener = BuildListener(purger, deadLetterHandler: deadLetterHandler);

        // A missing patient id fails validation, which raises a DeadLetterException.
        var result = BuildConsumeResult(patientId: string.Empty);

        await listener.ConsumeMessageAsync(result, CancellationToken.None);

        deadLetterHandler.Verify(
            item => item.HandleException(result, It.IsAny<DeadLetterException>(), FacilityId), Times.Once);
        purger.Verify(
            item => item.PurgeAsync(result.Message.Value, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeMessageAsync_TransientFailure_DoesNotPurgeTheResourceCache()
    {
        var purger = new Mock<IResourceCachePurger>();
        var transientHandler = new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(It.IsAny<ResourceCacheType>()))
            .Throws(new TransientException("cache is unreachable"));

        var listener = BuildListener(purger, transientHandler: transientHandler, resourceCache: resourceCache);

        await listener.ConsumeMessageAsync(BuildConsumeResult(), CancellationToken.None);

        transientHandler.Verify(
            item => item.HandleException(
                It.IsAny<ConsumeResult<ResourceKey, ResourcesAcquiredValue>>(),
                It.IsAny<TransientException>(),
                FacilityId),
            Times.Once);
        purger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsumeMessageAsync_UnexpectedFailure_DoesNotPurgeTheResourceCache()
    {
        var purger = new Mock<IResourceCachePurger>();
        var transientHandler = new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(It.IsAny<ResourceCacheType>()))
            .Throws(new InvalidOperationException("boom"));

        var listener = BuildListener(purger, transientHandler: transientHandler, resourceCache: resourceCache);

        await listener.ConsumeMessageAsync(BuildConsumeResult(), CancellationToken.None);

        // Unexpected exceptions are wrapped as transient and retried, so the cache must survive.
        transientHandler.Verify(
            item => item.HandleException(
                It.IsAny<ConsumeResult<ResourceKey, ResourcesAcquiredValue>>(),
                It.IsAny<TransientException>(),
                FacilityId),
            Times.Once);
        purger.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsumeMessageAsync_RecordsNormalizationDurationForTheMessage()
    {
        var purger = new Mock<IResourceCachePurger>();
        var metrics = new Mock<INormalizationServiceMetrics>();
        var resourceCache = new Mock<IResourceCache>();
        resourceCache
            .Setup(item => item.GetImplementation(It.IsAny<ResourceCacheType>()))
            .Throws(new TransientException("cache is unreachable"));

        var listener = BuildListener(purger, resourceCache: resourceCache, metrics: metrics);

        await listener.ConsumeMessageAsync(BuildConsumeResult(), CancellationToken.None);

        metrics.Verify(
            item => item.MeasureNormalizationDuration(It.Is<List<KeyValuePair<string, object?>>>(tags =>
                tags.Count == 2
                && tags.Exists(tag => tag.Key == DiagnosticNames.FacilityId && (tag.Value as string) == FacilityId)
                && tags.Exists(tag => tag.Key == DiagnosticNames.Phase && (tag.Value as string) == "Initial"))),
            Times.Once);
        metrics.Verify(
            item => item.MeasureNormalizationDuration(It.IsAny<List<KeyValuePair<string, object?>>>()),
            Times.Once);
    }

    private static ResourcesAcquiredListener BuildListener(
        Mock<IResourceCachePurger> purger,
        Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>? deadLetterHandler = null,
        Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>? transientHandler = null,
        Mock<IResourceCache>? resourceCache = null,
        Mock<INormalizationServiceMetrics>? metrics = null)
    {
        deadLetterHandler ??= new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        transientHandler ??= new Mock<ITransientExceptionHandler<ResourcesAcquiredListener, ResourceKey, ResourcesAcquiredValue>>();
        resourceCache ??= new Mock<IResourceCache>();
        metrics ??= new Mock<INormalizationServiceMetrics>();

        deadLetterHandler.SetupProperty(item => item.Topic);
        transientHandler.SetupProperty(item => item.Topic);

        var consumeExceptionHandler = new Mock<IDeadLetterExceptionHandler<ResourcesAcquiredListener, ResourceKey, string>>();
        consumeExceptionHandler.SetupProperty(item => item.Topic);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(item => item.CreateScope()).Returns(Mock.Of<IServiceScope>());

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
            metrics.Object,
            Mock.Of<IProducer<ResourceKey, ResourcesNormalizedValue>>(),
            new CopyPropertyOperationService(Mock.Of<ILogger<CopyPropertyOperationService>>()),
            new CodeMapOperationService(Mock.Of<ILogger<CodeMapOperationService>>()),
            new ConditionalTransformOperationService(Mock.Of<ILogger<ConditionalTransformOperationService>>()),
            new CopyLocationOperationService(Mock.Of<ILogger<CopyLocationOperationService>>()),
            new CopyLocationAliasToTypeIterativelyOperationService(Mock.Of<ILogger<CopyLocationAliasToTypeIterativelyOperationService>>()),
            new RemoveExtensionsOperationService(Mock.Of<ILogger<RemoveExtensionsOperationService>>()),
            resourceCache.Object,
            purger.Object,
            telemetrySettings.Object);
    }

    private static ConsumeResult<ResourceKey, ResourcesAcquiredValue> BuildConsumeResult(string patientId = PatientId)
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
                Key = new ResourceKey { FacilityId = FacilityId, PatientId = patientId },
                Value = new ResourcesAcquiredValue
                {
                    QueryType = "Initial",
                    ReportableEvent = "Adhoc",
                    ScheduledReports = new List<ScheduledReport> { new() { ReportTrackingId = "tracking-1" } },
                    CacheType = ResourceCacheType.Redis,
                    CacheKeys = new List<string> { $"{CorrelationId}:Patient" }
                }
            }
        };
    }
}
