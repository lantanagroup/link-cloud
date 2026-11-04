using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Handlers;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Listeners;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Submission.Application.Error;
using LantanaGroup.Link.Submission.KafkaProducers;
using Microsoft.Extensions.Logging;
using Moq;

namespace UnitTests.Submission;

[Trait("Category", "UnitTests")]
public class SubmitPayloadRetryDeadLetterHandlerTests
{
    private readonly Mock<IProducer<string, string>> _errorProducer = new();
    private readonly Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> _statusProducer = new();
    private readonly List<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> _statusMessages = [];

    public SubmitPayloadRetryDeadLetterHandlerTests()
    {
        _statusProducer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>((_, message, _) =>
                _statusMessages.Add(message))
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
    }

    [Fact]
    public void HandleException_WhenRetriesAreExhausted_PublishesPatientFailureWithTheAttemptMessage()
    {
        var reportId = Guid.Parse("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803");
        var handler = CreateHandler();
        var result = BuildResult(reportId, PayloadType.MeasureReportSubmissionEntry, "patient-123");

        handler.HandleException(
            result,
            new DeadLetterException($"Retry count exceeded for message with key: {result.Message.Key}"),
            "facility-1");

        var message = Assert.Single(_statusMessages);
        Assert.Equal(PatientReportingStatus.Submission, message.Value.Status);
        Assert.False(message.Value.IsSuccess);
        Assert.Equal("Failed to upload to external blob storage.", message.Value.ErrorMessage);
        Assert.Equal("patient-123", message.Value.PatientId);
        Assert.Equal(reportId.ToString(), message.Value.ReportScheduleId);
        Assert.Equal(reportId.ToString(), message.Key.ReportId);
        Assert.Equal("corr-1", message.Value.CorrelationId);
        Assert.Null(message.Value.ProcessingTime);
        _errorProducer.Verify(
            p => p.Produce(
                SubmitPayloadRetryDeadLetterHandler.ErrorTopic,
                It.IsAny<Message<string, string>>(),
                It.IsAny<Action<DeliveryReport<string, string>>>()),
            Times.Once);
    }

    [Fact]
    public void HandleException_ReportSchedule_DeadLettersWithoutAPatientStatus()
    {
        var handler = CreateHandler();
        var result = BuildResult(Guid.NewGuid(), PayloadType.ReportSchedule, patientId: null);

        handler.HandleException(result, new DeadLetterException("Retry count exceeded"), "facility-1");

        Assert.Empty(_statusMessages);
        _errorProducer.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<Action<DeliveryReport<string, string>>>()),
            Times.Once);
    }

    [Fact]
    public void HandleException_UnreadableRecord_DeadLettersWithoutAPatientStatus()
    {
        var handler = CreateHandler();
        var result = new ConsumeResult<string, string>
        {
            Message = new Message<string, string>
            {
                Key = "not-json",
                Value = "not-json",
                Headers = new Headers()
            }
        };

        handler.HandleException(result, new DeadLetterException("Retry count exceeded"), "facility-1");

        Assert.Empty(_statusMessages);
        _errorProducer.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<Action<DeliveryReport<string, string>>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("Failed to upload to external blob storage.\r\n   at Submission.Listener", "Failed to upload to external blob storage.")]
    [InlineData("Failed to download from internal blob storage.", "Failed to download from internal blob storage.")]
    public void TerminalErrorMessage_UsesTheLastAttemptNotTheRetryWrapper(string header, string expected)
    {
        var headers = new Headers
        {
            { KafkaConstants.HeaderConstants.RetryExceptionMessage, Encoding.UTF8.GetBytes(header) }
        };

        var message = SubmissionPatientStatus.TerminalErrorMessage(headers, "Retry count exceeded for message with key: abc");

        Assert.Equal(expected, message);
    }

    private SubmitPayloadRetryDeadLetterHandler CreateHandler()
    {
        var factory = new Mock<IKafkaProducerFactory<string, string>>();
        factory.Setup(f => f.CreateProducer(
                It.IsAny<ProducerConfig>(),
                It.IsAny<ISerializer<string>?>(),
                It.IsAny<ISerializer<string>?>(),
                It.IsAny<bool>()))
            .Returns(_errorProducer.Object);

        var inner = new DeadLetterExceptionHandler<RetryListener, string, string>(
            factory.Object,
            factory.Object,
            new ServiceInformation { ServiceName = "Submission", ServiceConfigName = "Submission" },
            new ExceptionLogger<RetryListener>(Mock.Of<ILogger<RetryListener>>()));

        var publisher = new SubmissionPatientStatusPublisher(
            new ReportStatusUpdatedProducer(_statusProducer.Object),
            Mock.Of<ILogger<SubmissionPatientStatusPublisher>>());

        return new SubmitPayloadRetryDeadLetterHandler(inner, publisher)
        {
            Topic = SubmitPayloadRetryDeadLetterHandler.ErrorTopic
        };
    }

    private static ConsumeResult<string, string> BuildResult(Guid reportId, PayloadType payloadType, string? patientId)
    {
        var key = new SubmitPayloadKey { FacilityId = "facility-1", ReportScheduleId = reportId };
        var value = new SubmitPayloadValue
        {
            PayloadType = payloadType,
            PayloadUri = "some/uri",
            ReportTypes = ["ACH"],
            PatientId = patientId
        };
        var keyContext = new SerializationContext(MessageComponentType.Key, "SubmitPayload-Retry");
        var valueContext = new SerializationContext(MessageComponentType.Value, "SubmitPayload-Retry");
        var headers = new Headers
        {
            { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes("corr-1") },
            {
                KafkaConstants.HeaderConstants.RetryExceptionMessage,
                Encoding.UTF8.GetBytes("Failed to upload to external blob storage.\n   at Submission.Listeners.SubmitPayloadListener")
            }
        };

        return new ConsumeResult<string, string>
        {
            Message = new Message<string, string>
            {
                Key = Encoding.UTF8.GetString(new JsonWithFhirMessageSerializer<SubmitPayloadKey>().Serialize(key, keyContext)),
                Value = Encoding.UTF8.GetString(new JsonWithFhirMessageSerializer<SubmitPayloadValue>().Serialize(value, valueContext)),
                Headers = headers
            }
        };
    }
}
