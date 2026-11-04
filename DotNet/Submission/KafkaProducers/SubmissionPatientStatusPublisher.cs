using System.Text;
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
    /// Final failure text. The shape is the simple exception type, then the cleaned message.
    /// ListenerExecutionFailedException and a "Retry count exceeded" exception are wrappers:
    /// the cause is used when one is present. A retry record that kept only the attempt
    /// message uses that message, and an exception-type header when the record has one.
    /// </summary>
    public static string TerminalErrorMessage(Exception? exception, Headers? headers, string? fallback)
    {
        var cause = UnderlyingCause(exception);
        var typeName = cause == null ? ExceptionTypeName(headers) : cause.GetType().Name;
        var rawMessage = cause == null ? HeaderOrFallbackMessage(headers, fallback) : cause.Message;
        return FormatErrorMessage(typeName, rawMessage);
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

    private const int MaxMessageLength = 200;
    private const string DefaultErrorMessage = "Submission failed.";
    private const string RetryExceededPrefix = "Retry count exceeded";
    private const string ListenerWrapperTypeName = "ListenerExecutionFailedException";

    /// <summary>
    /// Cause FQCN before the wrapper FQCN. .NET retry records do not write these;
    /// a Spring dead-letter record can.
    /// </summary>
    private static readonly string[] ExceptionTypeHeaders =
    [
        "kafka_dlt-exception-cause-fqcn",
        "kafka_exception-cause-fqcn",
        "kafka_dlt-exception-fqcn",
        "kafka_exception-fqcn",
        "X-Exception-Type",
        "X-Retry-Exception-Type"
    ];

    private static readonly string[] SecretMarkers =
    [
        "password=",
        "pwd=",
        "secret",
        "token",
        "apikey",
        "api_key",
        "accountkey",
        "sharedaccesssignature",
        "bearer "
    ];

    private static Exception? UnderlyingCause(Exception? exception)
    {
        var current = exception;
        var depth = 0;
        while (current != null && IsFrameworkWrapper(current))
        {
            var inner = current.InnerException;
            if (inner == null || ReferenceEquals(inner, current) || ++depth > 8)
            {
                return null;
            }

            current = inner;
        }

        return current;
    }

    private static bool IsFrameworkWrapper(Exception exception)
    {
        if (string.Equals(exception.GetType().Name, ListenerWrapperTypeName, StringComparison.Ordinal))
        {
            return true;
        }

        return exception.Message.StartsWith(RetryExceededPrefix, StringComparison.Ordinal);
    }

    private static string? ExceptionTypeName(Headers? headers)
    {
        if (headers == null)
        {
            return null;
        }

        foreach (var headerName in ExceptionTypeHeaders)
        {
            var simpleName = SimpleTypeName(ReadHeader(headers, headerName));
            if (string.IsNullOrEmpty(simpleName) || string.Equals(simpleName, ListenerWrapperTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            return simpleName;
        }

        return null;
    }

    private static string? SimpleTypeName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var lineBreak = IndexOfLineBreak(raw);
        var text = (lineBreak < 0 ? raw : raw[..lineBreak]).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var split = Math.Max(text.LastIndexOf('.'), text.LastIndexOf('$'));
        if (split >= 0 && split < text.Length - 1)
        {
            text = text[(split + 1)..].Trim();
        }

        return text.Length == 0 ? null : text;
    }

    private static string? HeaderOrFallbackMessage(Headers? headers, string? fallback)
    {
        var retryMessage = ReadHeader(headers, KafkaConstants.HeaderConstants.RetryExceptionMessage);
        if (!string.IsNullOrEmpty(NormalizeMessage(retryMessage)))
        {
            return retryMessage;
        }

        var exceptionMessage = ReadHeader(headers, KafkaConstants.HeaderConstants.ExceptionMessage);
        if (!string.IsNullOrEmpty(NormalizeMessage(exceptionMessage)))
        {
            return exceptionMessage;
        }

        return fallback;
    }

    private static string? ReadHeader(Headers? headers, string headerName)
    {
        if (headers == null || !headers.TryGetLastBytes(headerName, out var bytes) || bytes == null || bytes.Length == 0)
        {
            return null;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static string FormatErrorMessage(string? typeName, string? rawMessage)
    {
        var normalized = NormalizeMessage(rawMessage);
        var message = TruncateMessage(normalized);
        if (!string.IsNullOrEmpty(typeName))
        {
            if (string.IsNullOrEmpty(normalized) || ContainsSecret(normalized))
            {
                return typeName;
            }

            return $"{typeName}: {message}";
        }

        return string.IsNullOrEmpty(message) ? DefaultErrorMessage : message;
    }

    private static string? NormalizeMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lineBreak = IndexOfLineBreak(text);
        var line = lineBreak < 0 ? text : text[..lineBreak];
        var builder = new StringBuilder(line.Length);
        var pendingSpace = false;
        foreach (var character in line)
        {
            if (char.IsControl(character))
            {
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0)
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? TruncateMessage(string? normalized)
    {
        if (string.IsNullOrEmpty(normalized) || normalized.Length <= MaxMessageLength)
        {
            return normalized;
        }

        return string.Concat(normalized.AsSpan(0, MaxMessageLength - 3), "...");
    }

    private static bool ContainsSecret(string message)
    {
        foreach (var marker in SecretMarkers)
        {
            if (message.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfLineBreak(string text)
    {
        var length = text.Length;
        for (var index = 0; index < length; index++)
        {
            if (text[index] is '\r' or '\n')
            {
                return index;
            }
        }

        return -1;
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
        Exception? failure,
        string? fallbackError,
        CancellationToken cancellationToken)
    {
        if (key == null || !SubmissionPatientStatus.IsPatientSubmission(value))
        {
            return;
        }

        var errorMessage = SubmissionPatientStatus.TerminalErrorMessage(failure, sourceHeaders, fallbackError);
        var status = CreateValue(
            key,
            value!,
            sourceHeaders == null ? null : KafkaHeaderHelper.GetCorrelationId(sourceHeaders),
            isSuccess: false,
            errorMessage: errorMessage,
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
