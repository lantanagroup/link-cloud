using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Error;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

/// <summary>
/// When RetryListener exhausts the retry count for a ResourcesAcquired message it dead-letters the
/// message from shared code that knows nothing about the resource cache. This handler is the hook
/// that releases the cache on that second terminal path.
/// </summary>
[Trait("Category", "UnitTests")]
public class ResourcesAcquiredRetryDeadLetterHandlerTests
{
    private const string CorrelationId = "3fa85f64-5717-4562-b3fc-2c963f66afa6";

    [Fact]
    public void ProduceDeadLetter_PurgesTheCacheKeysCarriedOnTheRetriedMessage()
    {
        var purger = new Mock<IResourceCachePurger>();
        var handler = BuildHandler(purger, out var producer);

        var value = new ResourcesAcquiredValue
        {
            QueryType = "Initial",
            ReportableEvent = "Adhoc",
            ScheduledReports = new List<ScheduledReport> { new() { ReportTrackingId = "tracking-1" } },
            CacheType = ResourceCacheType.ABS,
            CacheKeys = new List<string> { $"{CorrelationId}:Patient", $"{CorrelationId}:Encounter" }
        };

        ResourcesAcquiredValue? purged = null;
        ResourceCachePurgeScope? scope = null;
        purger
            .Setup(item => item.PurgeAsync(It.IsAny<ResourcesAcquiredValue>(), It.IsAny<string>(), It.IsAny<ResourceCachePurgeScope>(), It.IsAny<CancellationToken>()))
            .Callback<ResourcesAcquiredValue?, string, ResourceCachePurgeScope, CancellationToken>((v, _, s, _) => { purged = v; scope = s; })
            .Returns(Task.CompletedTask);

        handler.ProduceDeadLetter(BuildRetryConsumeResult(JsonSerializer.Serialize(value)), "Retry count exceeded");

        Assert.NotNull(purged);
        Assert.Equal(value.CacheKeys, purged!.CacheKeys);
        Assert.Equal(ResourceCacheType.ABS, purged.CacheType);

        // Retry exhaustion cannot prove an earlier attempt did not already publish
        // ResourcesNormalized, so it must never remove {correlationId}.
        Assert.Equal(ResourceCachePurgeScope.AcquisitionKeysOnly, scope);

        // The dead letter is still produced: the durable record of the failure comes first.
        producer.Verify(
            item => item.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<Action<DeliveryReport<string, string>>>()),
            Times.Once);
    }

    [Fact]
    public void ProduceDeadLetter_WithUndeserializableValue_StillProducesTheDeadLetter()
    {
        var purger = new Mock<IResourceCachePurger>();
        var handler = BuildHandler(purger, out var producer);

        handler.ProduceDeadLetter(BuildRetryConsumeResult("this is not json"), "Retry count exceeded");

        purger.VerifyNoOtherCalls();
        producer.Verify(
            item => item.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<Action<DeliveryReport<string, string>>>()),
            Times.Once);
    }

    private static ResourcesAcquiredRetryDeadLetterHandler BuildHandler(
        Mock<IResourceCachePurger> purger,
        out Mock<IProducer<string, string>> producer)
    {
        producer = new Mock<IProducer<string, string>>();

        var producerFactory = new Mock<IKafkaProducerFactory<string, string>>();
        producerFactory
            .Setup(item => item.CreateProducer(
                It.IsAny<ProducerConfig>(),
                It.IsAny<ISerializer<string>>(),
                It.IsAny<ISerializer<string>>(),
                It.IsAny<bool>()))
            .Returns(producer.Object);

        return new ResourcesAcquiredRetryDeadLetterHandler(
            producerFactory.Object,
            producerFactory.Object,
            new ServiceInformation { ServiceConfigName = "Normalization" },
            Mock.Of<IExceptionLogger<RetryListener>>(),
            purger.Object,
            Mock.Of<ILogger<ResourcesAcquiredRetryDeadLetterHandler>>())
        {
            Topic = "ResourcesAcquired-Error"
        };
    }

    private static ConsumeResult<string, string> BuildRetryConsumeResult(string value) => new()
    {
        Topic = "ResourcesAcquired-Retry",
        Partition = new Partition(0),
        Offset = new Offset(0),
        Message = new Message<string, string>
        {
            Headers = new Headers(),
            Key = "key",
            Value = value
        }
    };
}
