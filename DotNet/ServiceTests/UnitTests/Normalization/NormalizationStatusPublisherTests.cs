using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Normalization.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using Microsoft.Extensions.Logging;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Normalization;

[Trait("Category", "UnitTests")]
public class NormalizationStatusPublisherTests
{
    [Theory]
    [InlineData("Initial", true, PatientReportingStatus.InitialNormalization)]
    [InlineData("INITIAL", false, PatientReportingStatus.InitialNormalization)]
    [InlineData("Supplemental", true, PatientReportingStatus.SupplementalNormalization)]
    [InlineData("SUPPLEMENTAL", false, PatientReportingStatus.SupplementalNormalization)]
    public async Task PublishesOneEventPerDistinctReportWithCanonicalKeyAndHeaders(
        string phase, bool success, PatientReportingStatus expectedStatus)
    {
        var (publisher, _, sent) = CreatePublisher();
        var headers = Headers();
        KafkaHeaderHelper.SetMetricsMode(headers, "performance");
        using var cts = new CancellationTokenSource();

        await publisher.PublishAsync(Key(), Value(phase), headers, success, 17,
            success ? null : "Normalization failed", cts.Token);

        Assert.Equal(2, sent.Count);
        Assert.Equal(new[] { "report-1", "report-2" }, sent.Select(m => m.Message.Key.ReportId));
        foreach (var (topic, message, token) in sent)
        {
            Assert.Equal("ReportStatusUpdated", topic);
            Assert.Equal(cts.Token, token);
            Assert.Equal(message.Key.ReportId, message.Value.ReportScheduleId);
            Assert.Equal("patient-1", message.Key.PatientId);
            Assert.Equal("patient-1", message.Value.PatientId);
            Assert.Equal(expectedStatus, message.Value.Status);
            Assert.Equal(success, message.Value.IsSuccess);
            Assert.Equal(17, message.Value.ProcessingTime);
            Assert.Equal(success ? null : "Normalization failed", message.Value.ErrorMessage);
            Assert.Equal("correlation-1", message.Value.CorrelationId);
            Assert.Equal("correlation-1", KafkaHeaderHelper.GetCorrelationId(message.Headers));
            Assert.Equal("performance", KafkaHeaderHelper.GetMetricsMode(message.Headers));
            Assert.Equal(TimeSpan.Zero, message.Value.Timestamp.Offset);
            Assert.Null(message.Value.Reportable);
            Assert.Null(message.Value.ValidationResult);
            var bytes = new JsonWithFhirMessageSerializer<ReportStatusUpdatedKey>().Serialize(
                message.Key, new SerializationContext(MessageComponentType.Key, topic));
            Assert.Equal($"{{\"reportId\":\"{message.Value.ReportScheduleId}\",\"patientId\":\"patient-1\"}}",
                Encoding.UTF8.GetString(bytes));
        }
        Assert.Equal(Partitioner.Murmur2Random, NormalizationStatusPublisher.CreateConfig().Partitioner);
    }

    [Fact]
    public async Task SuccessSendFailurePropagatesAndPreservesOccurrenceOnRetry()
    {
        var (publisher, producer, _) = CreatePublisher();
        var failure = new ProduceException<ReportStatusUpdatedKey, ReportStatusUpdatedValue>(
            new Error(ErrorCode.Local_MsgTimedOut), new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        producer.SetupSequence(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        var headers = Headers();
        var value = Value("Initial");
        value.ScheduledReports = [new() { ReportTrackingId = "report-1" }];
        await Assert.ThrowsAsync<ProduceException<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(() =>
            publisher.PublishAsync(Key(), value, headers, true, 17, null, CancellationToken.None));
        var timestampBytes = headers.First(h => h.Key == "X-Normalization-Completed-At").GetValueBytes();

        // Re-serialization of ResourcesAcquired-Retry retains headers, including completion time.
        var retryHeaders = new Confluent.Kafka.Headers();
        foreach (var header in headers)
            retryHeaders.Add(header.Key, header.GetValueBytes());
        await publisher.PublishAsync(Key(), value, retryHeaders, true, 99, null, CancellationToken.None);
        Assert.Equal(timestampBytes, retryHeaders.First(h => h.Key == "X-Normalization-Completed-At").GetValueBytes());
        // The sequence setup bypasses capture; inspect the actual argument of the second send.
        var message = (Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>)producer.Invocations.Last().Arguments[1];
        Assert.Equal(DateTimeOffset.Parse(Encoding.UTF8.GetString(timestampBytes)), message.Value.Timestamp);
        Assert.Equal(17, message.Value.ProcessingTime);
    }

    [Fact]
    public async Task TerminalFailureSendErrorDoesNotPreventOtherReportsFromBeingNotified()
    {
        var (publisher, producer, _) = CreatePublisher();
        producer.Setup(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        await publisher.PublishAsync(Key(), Value("Initial"), Headers(), false, null, "failed", CancellationToken.None);
        Assert.Equal(2, producer.Invocations.Count);
    }

    [Theory]
    [InlineData("Other", "patient-1", "correlation-1")]
    [InlineData("Initial", "", "correlation-1")]
    [InlineData("Initial", "patient-1", "")]
    public async Task CannotIdentifyStageOrPatient_DoesNotInventStatus(string phase, string patient, string correlation)
    {
        var (publisher, producer, _) = CreatePublisher();
        await publisher.PublishAsync(new ResourceKey { PatientId = patient }, Value(phase), Headers(correlation),
            false, null, "invalid input", CancellationToken.None);
        producer.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullCorrelationHeader_DoesNotPublishOrInterruptFailureHandling(bool success)
    {
        var (publisher, producer, _) = CreatePublisher();
        var headers = new Headers
        {
            { KafkaConstants.HeaderConstants.CorrelationId, null }
        };

        await publisher.PublishAsync(Key(), Value("Initial"), headers,
            success, null, "invalid input", CancellationToken.None);

        producer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationPropagatesEvenWhenReportingFailure()
    {
        var (publisher, producer, _) = CreatePublisher();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(
            Key(), Value("Initial"), Headers(), false, null, "failed", cts.Token));
        producer.VerifyNoOtherCalls();
    }

    private static ResourceKey Key() => new() { FacilityId = "facility-1", PatientId = "patient-1" };
    private static ResourcesAcquiredValue Value(string phase) => new()
    {
        QueryType = phase,
        ScheduledReports = [new() { ReportTrackingId = "report-1" }, new() { ReportTrackingId = "report-1" },
            new() { ReportTrackingId = "report-2" }, new() { ReportTrackingId = "" }]
    };
    private static Headers Headers(string correlation = "correlation-1") => new()
    {
        { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(correlation) }
    };

    private static (NormalizationStatusPublisher Publisher,
        Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> Producer,
        List<(string Topic, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue> Message, CancellationToken Token)> Sent) CreatePublisher()
    {
        var sent = new List<(string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken)>();
        var producer = new Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>();
        producer.Setup(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>(
                (topic, message, token) => sent.Add((topic, message, token)))
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        return (new NormalizationStatusPublisher(producer.Object, TimeProvider.System,
            Mock.Of<ILogger<NormalizationStatusPublisher>>()), producer, sent);
    }
}
