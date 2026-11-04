using System.Reflection;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Error.Exceptions;
using LantanaGroup.Link.Shared.Application.Error.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Submission.Application.Config;
using LantanaGroup.Link.Submission.Application.Interfaces;
using LantanaGroup.Link.Submission.KafkaProducers;
using LantanaGroup.Link.Submission.Listeners;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Submission;

[Trait("Category", "UnitTests")]
public class SubmitPayloadListenerTests
{
    private readonly Mock<ILogger<SubmitPayloadListener>> _loggerMock = new();
    private readonly Mock<IKafkaConsumerFactory<SubmitPayloadKey, SubmitPayloadValue>> _consumerFactoryMock = new();
    private readonly Mock<IConsumer<SubmitPayloadKey, SubmitPayloadValue>> _consumerMock = new();
    private readonly Mock<ITransientExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue>> _transientHandlerMock = new();
    private readonly Mock<IDeadLetterExceptionHandler<SubmitPayloadListener, SubmitPayloadKey, SubmitPayloadValue>> _deadLetterHandlerMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<ISubmissionServiceMetrics> _metricsMock = new();
    private readonly Mock<IProducer<PayloadSubmittedKey, PayloadSubmittedValue>> _payloadProducerMock = new();
    private readonly Mock<ILogger<AuditableEventOccurredProducer>> _auditLoggerMock = new();
    private readonly Mock<IProducer<string, AuditEventMessage>> _auditProducerMock = new();
    private readonly Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> _statusProducerMock = new();
    private readonly List<string> _publishOrder = [];

    public SubmitPayloadListenerTests()
    {
        _consumerFactoryMock
            .Setup(f => f.CreateConsumer(
                It.IsAny<ConsumerConfig>(),
                It.IsAny<IDeserializer<SubmitPayloadKey>?>(),
                It.IsAny<IDeserializer<SubmitPayloadValue>?>()))
            .Returns(_consumerMock.Object);

        _metricsMock
            .Setup(m => m.BuildTags(It.IsAny<string?>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns([]);

        _storageServiceMock.Setup(s => s.DestinationType).Returns("test");
        _storageServiceMock.Setup(s => s.HasInternalClient()).Returns(true);
        _storageServiceMock
            .Setup(s => s.DownloadFromInternalAsync(It.IsAny<SubmitPayloadValue>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        _storageServiceMock
            .Setup(s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _statusProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>((_, _, _) => _publishOrder.Add("status"))
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        _payloadProducerMock
            .Setup(p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<PayloadSubmittedKey, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>()))
            .Callback<string, Message<PayloadSubmittedKey, PayloadSubmittedValue>, Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>((_, _, _) => _publishOrder.Add("payload"));
    }

    private SubmitPayloadListener CreateListener(bool suppressManifest) =>
        new(
            _loggerMock.Object,
            _consumerFactoryMock.Object,
            _transientHandlerMock.Object,
            _deadLetterHandlerMock.Object,
            _storageServiceMock.Object,
            _metricsMock.Object,
            new PayloadSubmittedProducer(_payloadProducerMock.Object),
            new SubmissionPatientStatusPublisher(
                new ReportStatusUpdatedProducer(_statusProducerMock.Object),
                Mock.Of<ILogger<SubmissionPatientStatusPublisher>>()),
            new AuditableEventOccurredProducer(_auditLoggerMock.Object, _auditProducerMock.Object),
            Options.Create(new ExternalBlobStorageSettings { SuppressManifest = suppressManifest }));

    private static ConsumeResult<SubmitPayloadKey, SubmitPayloadValue> BuildConsumeResult(
        string facilityId,
        PayloadType payloadType,
        string? patientId = null,
        Guid? reportScheduleId = null,
        string? correlationId = null)
    {
        var headers = new Headers();
        if (correlationId != null)
        {
            headers.Add("X-Correlation-Id", Encoding.UTF8.GetBytes(correlationId));
        }

        return new()
        {
            Message = new Message<SubmitPayloadKey, SubmitPayloadValue>
            {
                Key = new SubmitPayloadKey
                {
                    FacilityId = facilityId,
                    ReportScheduleId = reportScheduleId ?? Guid.NewGuid()
                },
                Value = new SubmitPayloadValue
                {
                    PayloadType = payloadType,
                    PayloadUri = "some/uri",
                    ReportTypes = ["ACH"],
                    PatientId = patientId
                },
                Headers = headers
            }
        };
    }

    private static Task InvokeConsumeAsync(
        SubmitPayloadListener listener,
        ConsumeResult<SubmitPayloadKey, SubmitPayloadValue> result)
    {
        var method = typeof(SubmitPayloadListener)
            .GetMethod("ConsumeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(listener, [result, CancellationToken.None])!;
    }

    [Fact]
    public async Task ConsumeAsync_SuppressManifestTrue_ReportSchedule_SkipsBlobStorage()
    {
        var listener = CreateListener(suppressManifest: true);
        var result = BuildConsumeResult("facility-1", PayloadType.ReportSchedule);

        await InvokeConsumeAsync(listener, result);

        _storageServiceMock.Verify(
            s => s.DownloadFromInternalAsync(It.IsAny<SubmitPayloadValue>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _storageServiceMock.Verify(
            s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_SuppressManifestTrue_ReportSchedule_StillProducesPayloadSubmitted()
    {
        var listener = CreateListener(suppressManifest: true);
        var result = BuildConsumeResult("facility-1", PayloadType.ReportSchedule);

        await InvokeConsumeAsync(listener, result);

        _payloadProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.Is<Message<PayloadSubmittedKey, PayloadSubmittedValue>>(
                    m => m.Value.PayloadType == PayloadType.ReportSchedule),
                It.IsAny<Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_SuppressManifestFalse_ReportSchedule_PerformsBlobStorage()
    {
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult("facility-1", PayloadType.ReportSchedule);

        await InvokeConsumeAsync(listener, result);

        _storageServiceMock.Verify(
            s => s.DownloadFromInternalAsync(It.IsAny<SubmitPayloadValue>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageServiceMock.Verify(
            s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _payloadProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<PayloadSubmittedKey, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_SuppressManifestTrue_MeasureReportSubmissionEntry_PerformsBlobStorage()
    {
        var listener = CreateListener(suppressManifest: true);
        var result = BuildConsumeResult("facility-1", PayloadType.MeasureReportSubmissionEntry);

        await InvokeConsumeAsync(listener, result);

        _storageServiceMock.Verify(
            s => s.DownloadFromInternalAsync(It.IsAny<SubmitPayloadValue>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _storageServiceMock.Verify(
            s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _payloadProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<PayloadSubmittedKey, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_UploadSuccess_IncrementsUploadCountSuccess()
    {
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult("facility-1", PayloadType.MeasureReportSubmissionEntry);

        await InvokeConsumeAsync(listener, result);

        _metricsMock.Verify(m => m.IncrementUploadCount(It.IsAny<List<KeyValuePair<string, object?>>>(), "success"), Times.Once);
        _metricsMock.Verify(m => m.IncrementUploadCount(It.IsAny<List<KeyValuePair<string, object?>>>(), "failure"), Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_UploadFailure_IncrementsUploadCountFailure()
    {
        _storageServiceMock
            .Setup(s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upload failed"));

        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult("facility-1", PayloadType.MeasureReportSubmissionEntry);

        await InvokeConsumeAsync(listener, result);

        _metricsMock.Verify(m => m.IncrementUploadCount(It.IsAny<List<KeyValuePair<string, object?>>>(), "failure"), Times.Once);
        _metricsMock.Verify(m => m.IncrementUploadCount(It.IsAny<List<KeyValuePair<string, object?>>>(), "success"), Times.Never);
        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _transientHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<TransientException>(),
                It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_PatientUpload_PublishesSubmissionSuccessBeforePayloadSubmitted()
    {
        var reportId = Guid.Parse("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803");
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "facility-1",
            PayloadType.MeasureReportSubmissionEntry,
            patientId: "patient-123",
            reportScheduleId: reportId,
            correlationId: "corr-1");

        await InvokeConsumeAsync(listener, result);

        Assert.Equal(["status", "payload"], _publishOrder);
        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                nameof(KafkaTopic.ReportStatusUpdated),
                It.Is<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(m =>
                    m.Key.ReportId == reportId.ToString()
                    && m.Key.PatientId == "patient-123"
                    && m.Value.Status == PatientReportingStatus.Submission
                    && m.Value.IsSuccess
                    && m.Value.PatientId == "patient-123"
                    && m.Value.ReportScheduleId == reportId.ToString()
                    && m.Value.CorrelationId == "corr-1"
                    && m.Value.ProcessingTime >= 0
                    && m.Value.ErrorMessage == null
                    && m.Value.ValidationResult == null
                    && m.Value.Reportable == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_ReportSchedule_DoesNotPublishPatientStatus()
    {
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "facility-1",
            PayloadType.ReportSchedule,
            patientId: "patient-123");

        await InvokeConsumeAsync(listener, result);

        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(["payload"], _publishOrder);
    }

    [Fact]
    public async Task ConsumeAsync_PatientPayloadWithoutPatientId_DoesNotPublishStatus()
    {
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult("facility-1", PayloadType.MeasureReportSubmissionEntry, patientId: " ");

        await InvokeConsumeAsync(listener, result);

        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(["payload"], _publishOrder);
    }

    [Fact]
    public async Task ConsumeAsync_StatusSendFails_RetriesRecordAndSkipsPayloadSubmitted()
    {
        _statusProducerMock
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<ReportStatusUpdatedKey, ReportStatusUpdatedValue>(
                new Error(ErrorCode.Local_MsgTimedOut),
                new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>()));

        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "facility-1",
            PayloadType.MeasureReportSubmissionEntry,
            patientId: "patient-123");

        await InvokeConsumeAsync(listener, result);

        _payloadProducerMock.Verify(
            p => p.Produce(
                It.IsAny<string>(),
                It.IsAny<Message<PayloadSubmittedKey, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<PayloadSubmittedKey, PayloadSubmittedValue>>>()),
            Times.Never);
        _transientHandlerMock.Verify(
            h => h.HandleException(
                result,
                It.Is<TransientException>(ex => ex.Message == "Failed to publish submission status."),
                "facility-1"),
            Times.Once);
        _deadLetterHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<Exception>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_DeadLetter_PublishesSubmissionFailure()
    {
        var reportId = Guid.Parse("0b7e3f52-9c1d-4e8a-b2f6-71a4c5d9e803");
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "",
            PayloadType.MeasureReportSubmissionEntry,
            patientId: "patient-123",
            reportScheduleId: reportId,
            correlationId: "corr-1");

        await InvokeConsumeAsync(listener, result);

        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                nameof(KafkaTopic.ReportStatusUpdated),
                It.Is<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(m =>
                    m.Value.Status == PatientReportingStatus.Submission
                    && !m.Value.IsSuccess
                    && m.Value.ErrorMessage == "Facility ID not specified."
                    && m.Value.ProcessingTime == null
                    && m.Value.CorrelationId == "corr-1"
                    && m.Value.PatientId == "patient-123"
                    && m.Key.ReportId == reportId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _deadLetterHandlerMock.Verify(
            h => h.HandleException(
                result,
                It.Is<DeadLetterException>(ex => ex.Message == "Facility ID not specified."),
                It.IsAny<string>()),
            Times.Once);
        _transientHandlerMock.Verify(
            h => h.HandleException(
                It.IsAny<ConsumeResult<SubmitPayloadKey, SubmitPayloadValue>>(),
                It.IsAny<TransientException>(),
                It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ConsumeAsync_DeadLetterAfterRetry_UsesThisAttemptMessage()
    {
        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "",
            PayloadType.MeasureReportSubmissionEntry,
            patientId: "patient-123",
            correlationId: "corr-1");
        result.Message.Headers.Add(
            "X-Retry-Exception-Message",
            Encoding.UTF8.GetBytes("Failed to upload to external blob storage.\n   at Submission.Listeners.SubmitPayloadListener"));

        await InvokeConsumeAsync(listener, result);

        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                nameof(KafkaTopic.ReportStatusUpdated),
                It.Is<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(m =>
                    m.Value.ErrorMessage == "Facility ID not specified."),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ConsumeAsync_PatientUploadFails_DoesNotPublishStatusWhileRetryRemains()
    {
        _storageServiceMock
            .Setup(s => s.UploadToExternalAsync(It.IsAny<SubmitPayloadKey>(), It.IsAny<SubmitPayloadValue>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upload failed"));

        var listener = CreateListener(suppressManifest: false);
        var result = BuildConsumeResult(
            "facility-1",
            PayloadType.MeasureReportSubmissionEntry,
            patientId: "patient-123");

        await InvokeConsumeAsync(listener, result);

        _statusProducerMock.Verify(
            p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _transientHandlerMock.Verify(
            h => h.HandleException(
                result,
                It.Is<TransientException>(ex => ex.Message == "Failed to upload to external blob storage."),
                "facility-1"),
            Times.Once);
    }
}
