using System.Text;
using System.Globalization;
using Confluent.Kafka;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Domain;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Settings;
using LantanaGroup.Link.Shared.Application.Utilities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services;

public interface IAcquisitionStatusPublisher
{
    Task PublishIdentifiedAsync(DataAcquisitionRequested request, string correlationId, Headers headers,
        DateTimeOffset timestamp, CancellationToken cancellationToken);
    Task PublishCompletionAsync(TailCompletionResult result, Headers headers, CancellationToken cancellationToken);
}

/// <summary>Reports patient acquisition milestones; publication failures leave the tail recoverable.</summary>
public sealed class AcquisitionStatusPublisher(
    IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue> producer) : IAcquisitionStatusPublisher
{
    public static ProducerConfig CreateConfig() => new()
    {
        ClientId = "DataAcquisition_ReportStatusUpdated",
        CompressionType = CompressionType.Zstd,
        Partitioner = Partitioner.Murmur2Random
    };

    public Task PublishIdentifiedAsync(DataAcquisitionRequested request, string correlationId, Headers headers,
        DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.QueryType, "Initial", StringComparison.OrdinalIgnoreCase))
            return Task.CompletedTask;

        const string identifiedAtHeader = "X-Acquisition-Identified-At";
        if (headers.TryGetLastBytes(identifiedAtHeader, out var bytes) && bytes != null
            && DateTimeOffset.TryParse(Encoding.UTF8.GetString(bytes), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var originalTimestamp))
            timestamp = originalTimestamp;
        else
            headers.Add(identifiedAtHeader, Encoding.UTF8.GetBytes(timestamp.ToString("O")));

        return PublishAsync(request.ScheduledReports, request.PatientId, correlationId,
            PatientReportingStatus.PatientIdentified, true, null, null, timestamp, headers, cancellationToken);
    }

    public Task PublishCompletionAsync(TailCompletionResult result, Headers headers, CancellationToken cancellationToken)
    {
        PatientReportingStatus status;
        if (string.Equals(result.ResourcesAcquired.QueryType, "Initial", StringComparison.OrdinalIgnoreCase))
            status = PatientReportingStatus.InitialAcquisition;
        else if (string.Equals(result.ResourcesAcquired.QueryType, "Supplemental", StringComparison.OrdinalIgnoreCase))
            status = PatientReportingStatus.SupplementalAcquisition;
        else
            return Task.CompletedTask; // Census and other non-reporting phases do not report patient stages.

        var error = result.IsSuccess ? null : $"Acquisition failed for log ID(s): {string.Join(", ", result.FailedLogIds)}.";
        return PublishAsync(result.ResourcesAcquired.ScheduledReports, result.PatientId, result.CorrelationId,
            status, result.IsSuccess, result.ProcessingTime, error, result.Timestamp, headers, cancellationToken);
    }

    private async Task PublishAsync(IEnumerable<ScheduledReport>? reports, string patientId, string correlationId,
        PatientReportingStatus status, bool isSuccess, long? processingTime, string? errorMessage,
        DateTimeOffset timestamp, Headers sourceHeaders, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(patientId) || string.IsNullOrWhiteSpace(correlationId))
            return;

        foreach (var reportId in (reports ?? []).Where(report => report != null)
                     .Select(report => report.ReportTrackingId).Where(id => !string.IsNullOrWhiteSpace(id))
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headers = new Headers
            {
                { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(correlationId) }
            };
            KafkaHeaderHelper.CopyMetricsMode(sourceHeaders, headers);
            await producer.ProduceAsync(nameof(KafkaTopic.ReportStatusUpdated),
                new Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>
                {
                    Key = new ReportStatusUpdatedKey { ReportId = reportId!, PatientId = patientId },
                    Headers = headers,
                    Value = new ReportStatusUpdatedValue
                    {
                        ReportScheduleId = reportId!, PatientId = patientId, CorrelationId = correlationId,
                        Status = status, IsSuccess = isSuccess, ProcessingTime = processingTime,
                        ErrorMessage = errorMessage, Timestamp = timestamp
                    }
                }, cancellationToken);
        }
    }
}
