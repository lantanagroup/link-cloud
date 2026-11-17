using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
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
    private readonly Mock<IKafkaConsumerFactory<string, SubmitPayloadValue>> _consumerFactoryMock = new();
    private readonly Mock<IConsumer<string, SubmitPayloadValue>> _consumerMock = new();
    private readonly Mock<ITransientExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue>> _transientHandlerMock = new();
    private readonly Mock<IDeadLetterExceptionHandler<SubmitPayloadListener, string, SubmitPayloadValue>> _deadLetterHandlerMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<ISubmissionServiceMetrics> _metricsMock = new();
    private readonly Mock<IProducer<string, PayloadSubmittedValue>> _payloadProducerMock = new();
    private readonly Mock<ILogger<AuditableEventOccurredProducer>> _auditLoggerMock = new();
    private readonly Mock<IProducer<string, AuditEventMessage>> _auditProducerMock = new();

    public SubmitPayloadListenerTests()
    {
        _consumerFactoryMock
            .Setup(f => f.CreateConsumer(
                It.IsAny<ConsumerConfig>(),
                It.IsAny<IDeserializer<string>?>(),
                It.IsAny<IDeserializer<SubmitPayloadValue>?>(),
                It.IsAny<KafkaAssignmentTracker?>()))
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
            new AuditableEventOccurredProducer(_auditLoggerMock.Object, _auditProducerMock.Object),
            Options.Create(new ExternalBlobStorageSettings { SuppressManifest = suppressManifest }));

    private static ConsumeResult<string, SubmitPayloadValue> BuildConsumeResult(
        string facilityId, PayloadType payloadType) =>
        new()
        {
            Topic = nameof(KafkaTopic.SubmitPayload),
            Partition = 0,
            Offset = 0,
            Message = new Message<string, SubmitPayloadValue>
            {
                Key = facilityId + ":patient-1",
                Value = new SubmitPayloadValue
                {
                    FacilityId = facilityId,
                    PatientId = "patient-1",
                    ReportScheduleId = Guid.NewGuid(),
                    PayloadType = payloadType,
                    PayloadUri = "some/uri",
                    ReportTypes = ["ACH"]
                },
                Headers = new Headers()
            }
        };

    private static Task InvokeConsumeAsync(
        SubmitPayloadListener listener,
        ConsumeResult<string, SubmitPayloadValue> result)
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
                It.Is<Message<string, PayloadSubmittedValue>>(
                    m => m.Value.PayloadType == PayloadType.ReportSchedule
                         && m.Key == KafkaKeys.ForPatient("facility-1", "patient-1")
                         && m.Value.FacilityId == "facility-1"
                         && m.Value.PatientId == "patient-1"),
                It.IsAny<Action<DeliveryReport<string, PayloadSubmittedValue>>>()),
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
                It.IsAny<Message<string, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<string, PayloadSubmittedValue>>>()),
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
                It.IsAny<Message<string, PayloadSubmittedValue>>(),
                It.IsAny<Action<DeliveryReport<string, PayloadSubmittedValue>>>()),
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
    }
}
