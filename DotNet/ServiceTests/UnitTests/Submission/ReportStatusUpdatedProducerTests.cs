using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.KafkaProducers;
using Moq;

namespace UnitTests.Submission;

[Trait("Category", "UnitTests")]
public class ReportStatusUpdatedProducerTests
{
    private static readonly DateTimeOffset Occurrence = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateConfig_UsesJavaCompatiblePartitioner()
    {
        var config = ReportStatusUpdatedProducer.CreateConfig();

        Assert.Equal(Partitioner.Murmur2Random, config.Partitioner);
    }

    [Fact]
    public void Produce_Success_KeyBytesMatchSharedFixture_AndPayloadFields()
    {
        var (producer, captured) = CreateProducer();
        producer.Produce(SuccessValue(), SourceHeaders(), CancellationToken.None);

        var (topic, message) = Assert.Single(captured);
        Assert.Equal("ReportStatusUpdated", topic);
        var context = new SerializationContext(MessageComponentType.Key, "ReportStatusUpdated");
        var bytes = new JsonWithFhirMessageSerializer<ReportStatusUpdatedKey>().Serialize(message.Key, context);
        var expected = Encoding.UTF8.GetBytes(File.ReadAllText(FixturePath("key")).Trim());
        Assert.Equal(expected, bytes);

        Assert.Equal(PatientReportingStatus.Submission, message.Value.Status);
        Assert.True(message.Value.IsSuccess);
        Assert.Equal(15, message.Value.ProcessingTime);
        Assert.Null(message.Value.ErrorMessage);
        Assert.Equal("corr-1", message.Value.CorrelationId);
        Assert.Equal(Occurrence, message.Value.Timestamp);
        Assert.Null(message.Value.ValidationResult);
        Assert.Null(message.Value.Reportable);

        var json = Encoding.UTF8.GetString(new JsonWithFhirMessageSerializer<ReportStatusUpdatedValue>()
            .Serialize(message.Value, new SerializationContext(MessageComponentType.Value, "ReportStatusUpdated")));
        Assert.Contains("\"status\":\"Submission\"", json);
        Assert.Contains("\"isSuccess\":true", json);
        Assert.Contains("\"processingTime\":15", json);
        Assert.DoesNotContain("errorMessage", json);
        Assert.Equal("corr-1", Header(message, KafkaConstants.HeaderConstants.CorrelationId));
        Assert.Equal("performance", Header(message, KafkaConstants.HeaderConstants.MetricsMode));
    }

    [Fact]
    public void Produce_Failure_CarriesErrorMessageAndOmitsProcessingTime()
    {
        var (producer, captured) = CreateProducer();
        var value = SuccessValue();
        value.IsSuccess = false;
        value.ProcessingTime = null;
        value.ErrorMessage = "Failed to upload to external blob storage.";

        producer.Produce(value, null, CancellationToken.None);

        var (_, message) = Assert.Single(captured);
        Assert.False(message.Value.IsSuccess);
        Assert.Equal("Failed to upload to external blob storage.", message.Value.ErrorMessage);
        Assert.Equal(PatientReportingStatus.Submission, message.Value.Status);
        Assert.Null(message.Value.ProcessingTime);
        var json = Encoding.UTF8.GetString(new JsonWithFhirMessageSerializer<ReportStatusUpdatedValue>()
            .Serialize(message.Value, new SerializationContext(MessageComponentType.Value, "ReportStatusUpdated")));
        Assert.Contains("\"isSuccess\":false", json);
        Assert.Contains("\"errorMessage\":\"Failed to upload to external blob storage.\"", json);
        Assert.DoesNotContain("processingTime", json);
    }

    [Fact]
    public void Produce_WhenSendIsRetried_PreservesOccurrenceTimestampAndDoesNotResendItself()
    {
        var captured = new List<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>();
        var calls = 0;
        var mock = new Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>();
        mock.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>((_, message, _) =>
            {
                calls++;
                captured.Add(message);
                if (calls == 1)
                {
                    throw new ProduceException<ReportStatusUpdatedKey, ReportStatusUpdatedValue>(
                        new Error(ErrorCode.Local_MsgTimedOut),
                        new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
                }
            })
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());

        var producer = new ReportStatusUpdatedProducer(mock.Object);
        var value = SuccessValue();

        Assert.Throws<ProduceException<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(
            () => producer.Produce(value, null, CancellationToken.None));
        producer.Produce(value, null, CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.All(captured, message => Assert.Equal(Occurrence, message.Value.Timestamp));
        Assert.Same(value, captured[0].Value);
        Assert.Same(value, captured[1].Value);
        Assert.Equal(Occurrence, value.Timestamp);
    }

    private static (ReportStatusUpdatedProducer Producer, List<(string Topic, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue> Message)> Captured) CreateProducer()
    {
        var captured = new List<(string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>)>();
        var mock = new Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>();
        mock.Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>((topic, message, _) =>
                captured.Add((topic, message)))
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        return (new ReportStatusUpdatedProducer(mock.Object), captured);
    }

    private static ReportStatusUpdatedValue SuccessValue() => new()
    {
        ReportScheduleId = "0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803",
        PatientId = "patient-123",
        Status = PatientReportingStatus.Submission,
        IsSuccess = true,
        ProcessingTime = 15,
        CorrelationId = "corr-1",
        Timestamp = Occurrence
    };

    private static Headers SourceHeaders()
    {
        var headers = new Headers
        {
            { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes("corr-1") }
        };
        KafkaHeaderHelper.SetMetricsMode(headers, "performance");
        return headers;
    }

    private static string? Header(Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue> message, string name)
    {
        if (!message.Headers.TryGetLastBytes(name, out var bytes))
        {
            return null;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "ReportStatus", name + ".json");
}
