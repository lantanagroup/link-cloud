using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Error;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using System.Text;
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
        purger
            .Setup(item => item.PurgeAsync(It.IsAny<ResourcesAcquiredValue>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ResourcesAcquiredValue?, string, CancellationToken>((v, _, _) => purged = v)
            .Returns(Task.CompletedTask);

        handler.ProduceDeadLetter(BuildRetryConsumeResult(JsonSerializer.Serialize(value)), "Retry count exceeded");

        Assert.NotNull(purged);
        Assert.Equal(value.CacheKeys, purged!.CacheKeys);
        Assert.Equal(ResourceCacheType.ABS, purged.CacheType);

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

    [Theory]
    [InlineData("Initial")]
    [InlineData("Supplemental")]
    public void RetryExhaustionReportsOriginalFailureBeforePurging(string phase)
    {
        var status = new Mock<INormalizationStatusPublisher>();
        var purger = new Mock<IResourceCachePurger>();
        var order = new List<string>();
        status.Setup(p => p.PublishAsync(It.IsAny<ResourceKey>(), It.IsAny<ResourcesAcquiredValue>(),
                It.IsAny<Headers>(), false, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("status"))
            .Returns(Task.CompletedTask);
        purger.Setup(p => p.PurgeAsync(It.IsAny<ResourcesAcquiredValue>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("purge"))
            .Returns(Task.CompletedTask);
        var handler = BuildHandler(purger, out _, status.Object);
        var result = BuildRetryConsumeResult(JsonSerializer.Serialize(new ResourcesAcquiredValue
        {
            QueryType = phase,
            ScheduledReports = [new() { ReportTrackingId = "report-1" }],
            CacheKeys = []
        }));
        result.Message.Key = JsonSerializer.Serialize(new ResourceKey { PatientId = "patient-1", FacilityId = "facility-1" });
        result.Message.Headers.Add(KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(CorrelationId));
        result.Message.Headers.Add(KafkaConstants.HeaderConstants.RetryExceptionMessage,
            Encoding.UTF8.GetBytes("Original normalization error\r\nstack trace"));
        handler.ProduceDeadLetter(result, "Retry count exceeded");
        Assert.Equal(new[] { "status", "purge" }, order);
        status.Verify(p => p.PublishAsync(It.Is<ResourceKey>(k => k.PatientId == "patient-1"),
            It.Is<ResourcesAcquiredValue>(v => v.QueryType == phase), result.Message.Headers,
            false, null, "Original normalization error", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProduceDeadLetter_WithMissingOrNullErrorHeader_UsesExceptionMessageAndPurges(bool includeNullHeader)
    {
        var status = new Mock<INormalizationStatusPublisher>();
        var purger = new Mock<IResourceCachePurger>();
        var handler = BuildHandler(purger, out _, status.Object);
        var result = BuildRetryConsumeResult(JsonSerializer.Serialize(new ResourcesAcquiredValue { CacheKeys = [] }));
        if (includeNullHeader)
            result.Message.Headers.Add(KafkaConstants.HeaderConstants.RetryExceptionMessage, null);

        handler.ProduceDeadLetter(result, "Retry count exceeded");

        status.Verify(p => p.PublishAsync(It.IsAny<ResourceKey>(), It.IsAny<ResourcesAcquiredValue>(),
            result.Message.Headers, false, null, "Retry count exceeded", It.IsAny<CancellationToken>()), Times.Once);
        purger.Verify(p => p.PurgeAsync(It.IsAny<ResourcesAcquiredValue>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProduceDeadLetter_WhenStatusPublishingFails_LogsAndStillPurges(bool throwSynchronously)
    {
        var status = new Mock<INormalizationStatusPublisher>();
        var purger = new Mock<IResourceCachePurger>();
        var logger = new Mock<ILogger<ResourcesAcquiredRetryDeadLetterHandler>>();
        var failure = new InvalidOperationException("Status publisher failed");
        var order = new List<string>();
        var publish = status.Setup(p => p.PublishAsync(It.IsAny<ResourceKey>(), It.IsAny<ResourcesAcquiredValue>(),
                It.IsAny<Headers>(), false, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("status"));
        if (throwSynchronously)
            publish.Throws(failure);
        else
            publish.Returns(Task.FromException(failure));
        purger.Setup(p => p.PurgeAsync(It.IsAny<ResourcesAcquiredValue>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("purge"))
            .Returns(Task.CompletedTask);
        var handler = BuildHandler(purger, out var producer, status.Object, logger.Object);
        var value = new ResourcesAcquiredValue { CacheKeys = ["cache-key"] };

        handler.ProduceDeadLetter(BuildRetryConsumeResult(JsonSerializer.Serialize(value)), "Retry count exceeded");

        Assert.Equal(new[] { "status", "purge" }, order);
        purger.Verify(p => p.PurgeAsync(It.Is<ResourcesAcquiredValue>(v => v.CacheKeys.SequenceEqual(value.CacheKeys)),
            "retry count exhausted: Retry count exceeded", It.IsAny<CancellationToken>()), Times.Once);
        producer.Verify(p => p.Produce(It.IsAny<string>(), It.IsAny<Message<string, string>>(),
            It.IsAny<Action<DeliveryReport<string, string>>>()), Times.Once);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to publish normalization failure")),
            failure, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    private static ResourcesAcquiredRetryDeadLetterHandler BuildHandler(
        Mock<IResourceCachePurger> purger,
        out Mock<IProducer<string, string>> producer,
        INormalizationStatusPublisher? statusPublisher = null,
        ILogger<ResourcesAcquiredRetryDeadLetterHandler>? logger = null)
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
            logger ?? Mock.Of<ILogger<ResourcesAcquiredRetryDeadLetterHandler>>(),
            statusPublisher ?? Mock.Of<INormalizationStatusPublisher>())
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
