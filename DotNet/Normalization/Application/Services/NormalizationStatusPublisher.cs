using System.Globalization;
using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Normalization.Application.Models.Messages;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Normalization.Application.Services;

public interface INormalizationStatusPublisher
{
    Task PublishAsync(ResourceKey? key, ResourcesAcquiredValue? value, Headers? sourceHeaders,
        bool isSuccess, long? processingTime, string? errorMessage, CancellationToken cancellationToken);
}

/// <summary>Reports bundle normalization completion for each distinct scheduled report.</summary>
public sealed class NormalizationStatusPublisher(
    IProducer<ReportStatusUpdatedKey, ReportStatusUpdatedValue> producer,
    TimeProvider timeProvider,
    ILogger<NormalizationStatusPublisher> logger) : INormalizationStatusPublisher
{
    // Retained on ResourcesAcquired-Retry if publishing completion or the downstream event fails.
    private const string CompletionTimeHeader = "X-Normalization-Completed-At";
    private const string DurationHeader = "X-Normalization-Processing-Time";

    public static ProducerConfig CreateConfig() => new()
    {
        ClientId = "Normalization_ReportStatusUpdated",
        Partitioner = Partitioner.Murmur2Random
    };

    public async Task PublishAsync(ResourceKey? key, ResourcesAcquiredValue? value, Headers? sourceHeaders,
        bool isSuccess, long? processingTime, string? errorMessage, CancellationToken cancellationToken)
    {
        var correlationId = sourceHeaders == null ? null : KafkaHeaderHelper.GetCorrelationId(sourceHeaders);
        PatientReportingStatus status;
        if (string.Equals(value?.QueryType, "Initial", StringComparison.OrdinalIgnoreCase))
            status = PatientReportingStatus.InitialNormalization;
        else if (string.Equals(value?.QueryType, "Supplemental", StringComparison.OrdinalIgnoreCase))
            status = PatientReportingStatus.SupplementalNormalization;
        else
            return; // An unknown phase cannot identify a completed/failed normalization stage.

        if (string.IsNullOrWhiteSpace(key?.PatientId) || string.IsNullOrWhiteSpace(correlationId))
            return; // Malformed messages still dead-letter, but cannot produce a valid patient event.

        var timestamp = timeProvider.GetUtcNow();
        if (isSuccess && sourceHeaders != null)
        {
            if (sourceHeaders.TryGetLastBytes(CompletionTimeHeader, out var timestampBytes)
                && DateTimeOffset.TryParse(Encoding.UTF8.GetString(timestampBytes), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var completedAt))
            {
                timestamp = completedAt;
                if (sourceHeaders.TryGetLastBytes(DurationHeader, out var durationBytes)
                    && long.TryParse(Encoding.UTF8.GetString(durationBytes), out var duration))
                    processingTime = duration;
            }
            else
            {
                sourceHeaders.Add(CompletionTimeHeader, Encoding.UTF8.GetBytes(timestamp.ToString("O")));
                if (processingTime.HasValue)
                    sourceHeaders.Add(DurationHeader, Encoding.UTF8.GetBytes(processingTime.Value.ToString(CultureInfo.InvariantCulture)));
            }
        }

        foreach (var reportId in (value!.ScheduledReports ?? [])
                     .Where(report => report != null)
                     .Select(report => report.ReportTrackingId)
                     .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headers = new Headers
            {
                { KafkaConstants.HeaderConstants.CorrelationId, Encoding.UTF8.GetBytes(correlationId) }
            };
            KafkaHeaderHelper.CopyMetricsMode(sourceHeaders, headers);
            var message = new Message<ReportStatusUpdatedKey, ReportStatusUpdatedValue>
            {
                Key = new ReportStatusUpdatedKey { ReportId = reportId!, PatientId = key.PatientId },
                Headers = headers,
                Value = new ReportStatusUpdatedValue
                {
                    ReportScheduleId = reportId!,
                    PatientId = key.PatientId,
                    Status = status,
                    IsSuccess = isSuccess,
                    ProcessingTime = processingTime,
                    ErrorMessage = isSuccess ? null : errorMessage,
                    CorrelationId = correlationId,
                    Timestamp = timestamp
                }
            };

            try
            {
                await producer.ProduceAsync(nameof(KafkaTopic.ReportStatusUpdated), message, cancellationToken);
            }
            catch (Exception ex) when (!isSuccess && ex is not OperationCanceledException)
            {
                // As with Submission, a failed diagnostic send must not prevent terminal cleanup.
                logger.LogError(ex, "Failed to publish normalization failure for report {ReportId}, patient {PatientId}.",
                    reportId.SanitizeForLog(), key.PatientId.SanitizeForLog());
            }
        }
    }
}
