using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Utilities;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class AcquisitionStatusPublisherTests
{
    private readonly Mock<IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> _producer = new();
    private readonly List<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>> _sent = [];
    private readonly AcquisitionStatusPublisher _publisher;
    private static readonly DateTimeOffset OccurredAt = DateTimeOffset.Parse("2026-11-10T15:00:00Z");

    public AcquisitionStatusPublisherTests()
    {
        _producer.Setup(p => p.ProduceAsync("ReportStatusUpdated",
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>, CancellationToken>(
                (_, message, _) => _sent.Add(message))
            .ReturnsAsync(new DeliveryResult<ReportStatusUpdatedKey, ReportStatusUpdatedValue>());
        _publisher = new AcquisitionStatusPublisher(_producer.Object);
    }

    [Theory]
    [InlineData("Initial", true, PatientReportingStatus.InitialAcquisition)]
    [InlineData("INITIAL", false, PatientReportingStatus.InitialAcquisition)]
    [InlineData("Supplemental", true, PatientReportingStatus.SupplementalAcquisition)]
    [InlineData("SUPPLEMENTAL", false, PatientReportingStatus.SupplementalAcquisition)]
    public async Task CompletionReportsEachDistinctScheduleUsingCanonicalKeyAndOccurrence(
        string phase, bool success, PatientReportingStatus expectedStatus)
    {
        var result = Tail(phase);
        if (!success) result.FailedLogIds = [42, 63];
        var headers = new Headers();
        KafkaHeaderHelper.SetMetricsMode(headers, "performance");
        using var cts = new CancellationTokenSource();

        await _publisher.PublishCompletionAsync(result, headers, cts.Token);

        Assert.Equal(2, _sent.Count);
        Assert.Equal(new[] { "report-1", "report-2" }, _sent.Select(m => m.Key.ReportId));
        foreach (var message in _sent)
        {
            Assert.Equal(message.Key.ReportId, message.Value.ReportScheduleId);
            Assert.Equal("patient-1", message.Key.PatientId);
            Assert.Equal("patient-1", message.Value.PatientId);
            Assert.Equal(expectedStatus, message.Value.Status);
            Assert.Equal(success, message.Value.IsSuccess);
            Assert.Equal(OccurredAt, message.Value.Timestamp);
            Assert.Equal(125L, message.Value.ProcessingTime);
            Assert.Equal("correlation-1", message.Value.CorrelationId);
            Assert.Equal("correlation-1", KafkaHeaderHelper.GetCorrelationId(message.Headers));
            Assert.Equal("performance", KafkaHeaderHelper.GetMetricsMode(message.Headers));
            if (success) Assert.Null(message.Value.ErrorMessage);
            else Assert.Equal("Acquisition failed for log ID(s): 42, 63.", message.Value.ErrorMessage);
            Assert.Null(message.Value.Reportable);
            Assert.Null(message.Value.ValidationResult);
            var bytes = new JsonWithFhirMessageSerializer<ReportStatusUpdatedKey>().Serialize(
                message.Key, new SerializationContext(MessageComponentType.Key, "ReportStatusUpdated"));
            Assert.Equal($"{{\"reportId\":\"{message.Key.ReportId}\",\"patientId\":\"patient-1\"}}", Encoding.UTF8.GetString(bytes));
        }
        Assert.All(_producer.Invocations, invocation => Assert.Equal(cts.Token, invocation.Arguments[2]));
        Assert.Equal(Partitioner.Murmur2Random, AcquisitionStatusPublisher.CreateConfig().Partitioner);
    }

    [Theory]
    [InlineData("Initial")]
    [InlineData("Supplemental")]
    public async Task IdentifiedAndCompletionNormalizeReportIdsButPreservePatientKeys(string phase)
    {
        const string reportId = "abcdef12-3456-7890-abcd-ef1234567890";
        const string patientId = "Patient/MixedCase-ID";
        var request = new DataAcquisitionRequested
        {
            PatientId = patientId, QueryType = "Initial",
            ScheduledReports = [new() { ReportTrackingId = reportId.ToUpperInvariant() },
                new() { ReportTrackingId = reportId }]
        };
        var tail = Tail(phase);
        tail.PatientId = patientId;
        tail.ResourcesAcquired.ScheduledReports = [new() { ReportTrackingId = reportId },
            new() { ReportTrackingId = reportId.ToUpperInvariant() }];

        await _publisher.PublishIdentifiedAsync(request, "correlation-1", new Headers(), OccurredAt, CancellationToken.None);
        await _publisher.PublishCompletionAsync(tail, new Headers(), CancellationToken.None);

        Assert.Equal(2, _sent.Count);
        Assert.All(_sent, message =>
        {
            Assert.Equal(reportId, message.Key.ReportId);
            Assert.Equal(reportId, message.Value.ReportScheduleId);
            Assert.Equal(patientId, message.Key.PatientId);
            Assert.Equal(patientId, message.Value.PatientId);
        });
        var serializer = new JsonWithFhirMessageSerializer<ReportStatusUpdatedKey>();
        var context = new SerializationContext(MessageComponentType.Key, "ReportStatusUpdated");
        Assert.Equal(serializer.Serialize(_sent[0].Key, context), serializer.Serialize(_sent[1].Key, context));
    }

    [Fact]
    public async Task PatientIdentifiedIsInitialOnlyAndPreservesTimestampAcrossRequestRetries()
    {
        var request = new DataAcquisitionRequested
        {
            PatientId = "patient-1", QueryType = "INITIAL", ScheduledReports = Reports()
        };
        var headers = new Headers();
        await _publisher.PublishIdentifiedAsync(request, "correlation-1", headers, OccurredAt, CancellationToken.None);
        await _publisher.PublishIdentifiedAsync(request, "correlation-1", headers, OccurredAt.AddHours(1), CancellationToken.None);
        Assert.Equal(4, _sent.Count);
        Assert.All(_sent, message =>
        {
            Assert.Equal(PatientReportingStatus.PatientIdentified, message.Value.Status);
            Assert.True(message.Value.IsSuccess);
            Assert.Equal(OccurredAt, message.Value.Timestamp);
            Assert.Null(message.Value.ProcessingTime);
        });
        request.QueryType = "Supplemental";
        await _publisher.PublishIdentifiedAsync(request, "correlation-1", headers, OccurredAt, CancellationToken.None);
        Assert.Equal(4, _sent.Count);
    }

    [Theory]
    [InlineData("Polling", "patient-1", "correlation-1")]
    [InlineData("Initial", "", "correlation-1")]
    [InlineData("Supplemental", "patient-1", "")]
    public async Task InvalidIdentityOrNonReportingPhaseDoesNotPublish(string phase, string patient, string correlation)
    {
        var tail = Tail(phase);
        tail.PatientId = patient;
        tail.CorrelationId = correlation;
        await _publisher.PublishCompletionAsync(tail, new Headers(), CancellationToken.None);
        Assert.Empty(_sent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublicationFailurePropagatesSoTailCanBeRecovered(bool success)
    {
        _producer.Setup(p => p.ProduceAsync(It.IsAny<string>(),
                It.IsAny<Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var tail = Tail("Initial");
        if (!success) tail.FailedLogIds = [42];
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _publisher.PublishCompletionAsync(tail, new Headers(), CancellationToken.None));
    }

    private static List<ScheduledReport> Reports() =>
        [new() { ReportTrackingId = "report-1" }, new() { ReportTrackingId = "report-1" },
            new() { ReportTrackingId = "" }, new() { ReportTrackingId = "report-2" }];

    private static TailCompletionResult Tail(string phase) => new()
    {
        PatientId = "patient-1", CorrelationId = "correlation-1", Timestamp = OccurredAt, ProcessingTime = 125,
        ResourcesAcquired = new ResourcesAcquired { QueryType = phase, ScheduledReports = Reports() }
    };
}
