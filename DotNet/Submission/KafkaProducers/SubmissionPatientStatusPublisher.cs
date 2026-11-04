using System.Text.Json;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Application.Enums;
using LantanaGroup.Link.Shared.Application.Enums.Reporting;
using LantanaGroup.Link.Shared.Application.Models.Kafka;
using LantanaGroup.Link.Shared.Application.SerDes;
using LantanaGroup.Link.Shared.Application.Services.Security;
using LantanaGroup.Link.Shared.Application.Utilities;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Submission.KafkaProducers;

/// <summary>
/// Patient submission facts the status event is allowed to carry.
/// Report schedules are the manifest and are not a patient stage.
/// </summary>
public static class SubmissionPatientStatus
{
    public static bool IsPatientSubmission(SubmitPayloadValue? value) =>
        value is { PayloadType: PayloadType.MeasureReportSubmissionEntry, PatientId: { } patientId }
        && !string.IsNullOrWhiteSpace(patientId);

    /// <summary>
    /// The last attempt's message wins. The retry header stores that message, then a newline, then the stack.
    /// "Retry count exceeded" is the wrapper the retry listener throws once the attempts are used up.
    /// </summary>
    public static string TerminalErrorMessage(Headers? headers, string? fallback)
    {
        var retryMessage = FirstLine(headers, KafkaConstants.HeaderConstants.RetryExceptionMessage);
        if (!string.IsNullOrWhiteSpace(retryMessage))
        {
            return retryMessage;
        }

        var exceptionMessage = FirstLine(headers, KafkaConstants.HeaderConstants.ExceptionMessage);
        if (!string.IsNullOrWhiteSpace(exceptionMessage))
        {
            return exceptionMessage;
        }

        var fallbackLine = FirstLine(fallback);
        return string.IsNullOrWhiteSpace(fallbackLine) ? "Submission failed." : fallbackLine;
    }

    public static bool TryReadPayload(string? keyJson, string? valueJson, out SubmitPayloadKey? key, out SubmitPayloadValue? value)
    {
        key = null;
        value = null;
        if (string.IsNullOrWhiteSpace(keyJson) || string.IsNullOrWhiteSpace(valueJson))
        {
            return false;
        }

        try
        {
            key = JsonSerializer.Deserialize<SubmitPayloadKey>(keyJson, LinkFhirSerializerOptions.ForFhirLenientSerialization);
            value = JsonSerializer.Deserialize<SubmitPayloadValue>(valueJson, LinkFhirSerializerOptions.ForFhirLenientSerialization);
            return key != null && value != null;
        }
        catch (JsonException)
        {
            key = null;
            value = null;
            return false;
        }
    }

    private static string? FirstLine(Headers? headers, string headerName)
    {
        if (headers == null || !headers.TryGetLastBytes(headerName, out var bytes) || bytes == null)
        {
            return null;
        }

        return FirstLine(System.Text.Encoding.UTF8.GetString(bytes));
    }

    internal static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var line = text.Split('\n', 2)[0].TrimEnd('\r').Trim();
        return string.IsNullOrEmpty(line) ? null : line;
    }
}

public sealed class SubmissionPatientStatusPublisher(
    ReportStatusUpdatedProducer producer,
    ILogger<SubmissionPatientStatusPublisher> logger)
{
    public void PublishSuccess(
        SubmitPayloadKey key,
        SubmitPayloadValue value,
        string? correlationId,
        long processingTime,
        Headers? sourceHeaders,
        CancellationToken cancellationToken)
    {
        if (!SubmissionPatientStatus.IsPatientSubmission(value))
        {
            return;
        }

        var status = CreateValue(key, value, correlationId, isSuccess: true, errorMessage: null, processingTime);
        producer.Produce(status, sourceHeaders, cancellationToken);
        LogPublished(status);
    }

    public void PublishTerminalFailure(
        SubmitPayloadKey? key,
        SubmitPayloadValue? value,
        Headers? sourceHeaders,
        string? fallbackError,
        CancellationToken cancellationToken)
    {
        if (key == null || !SubmissionPatientStatus.IsPatientSubmission(value))
        {
            return;
        }

        var errorMessage = SubmissionPatientStatus.FirstLine(fallbackError);
        var status = CreateValue(
            key,
            value!,
            sourceHeaders == null ? null : KafkaHeaderHelper.GetCorrelationId(sourceHeaders),
            isSuccess: false,
            errorMessage: string.IsNullOrWhiteSpace(errorMessage) ? "Submission failed." : errorMessage,
            processingTime: null);

        try
        {
            producer.Produce(status, sourceHeaders, cancellationToken);
            LogPublished(status);
        }
        catch (Exception ex)
        {
            // The caller still dead-letters. Producer retries already ran inside the send.
            logger.LogError(
                ex,
                "Failed to publish submission failure status for report {ReportId} patient {PatientId}.",
                key.ReportScheduleId,
                value!.PatientId.SanitizeForLog());
        }
    }

    private void LogPublished(ReportStatusUpdatedValue status)
    {
        logger.LogInformation(
            "Published submission status {Outcome} for report {ReportId} patient {PatientId}.",
            status.IsSuccess ? "success" : "failure",
            status.ReportScheduleId.SanitizeForLog(),
            status.PatientId.SanitizeForLog());
    }

    private static ReportStatusUpdatedValue CreateValue(
        SubmitPayloadKey key,
        SubmitPayloadValue value,
        string? correlationId,
        bool isSuccess,
        string? errorMessage,
        long? processingTime) =>
        new()
        {
            ReportScheduleId = key.ReportScheduleId.ToString(),
            PatientId = value.PatientId!,
            Status = PatientReportingStatus.Submission,
            IsSuccess = isSuccess,
            ProcessingTime = processingTime,
            ErrorMessage = errorMessage,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString() : correlationId,
            Timestamp = DateTimeOffset.UtcNow
        };
}
