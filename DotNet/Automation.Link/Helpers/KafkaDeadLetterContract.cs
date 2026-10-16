using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Settings;
using Newtonsoft.Json.Linq;

namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Canonical Kafka dead-letter / retry header and payload shapes used by Link services.
/// .NET listeners write <see cref="KafkaConstants.HeaderConstants"/> via
/// DeadLetterExceptionHandler / TransientExceptionHandler. Java Spring Kafka DLT recoverers
/// write the kafka_dlt-* / kafka_exception-* headers. ResourceNormalized (and ReadyForValidation)
/// keys are JSON objects, not the raw facility id string used on most other topics.
/// </summary>
public static class KafkaDeadLetterContract
{
    public const int DefaultValuePreviewLength = 500;
    public const int DefaultHeaderPreviewLength = 100;
    public const int ExceptionHeaderPreviewLength = 12000;
    public const int ResourcesNormalizedValuePreviewLength = 4000;
    public const int ResourcesNormalizedHeaderPreviewLength = 2000;

    /// <summary>
    /// Spring Kafka DeadLetterPublishingRecoverer / DefaultErrorHandler headers.
    /// Kept in sync with Admin.BFF KafkaConsumerService and Spring Kafka's KafkaHeaders.
    /// </summary>
    public static class SpringKafkaHeaders
    {
        public const string DltExceptionMessage = "kafka_dlt-exception-message";
        public const string DltExceptionStackTrace = "kafka_dlt-exception-stacktrace";
        public const string DltExceptionFqcn = "kafka_dlt-exception-fqcn";
        public const string DltExceptionCauseFqcn = "kafka_dlt-exception-cause-fqcn";
        public const string ExceptionMessage = "kafka_exception-message";
        public const string ExceptionStackTrace = "kafka_exception-stacktrace";
        public const string ExceptionFqcn = "kafka_exception-fqcn";
        public const string ExceptionCauseFqcn = "kafka_exception-cause-fqcn";
    }

    public static readonly string[] ExceptionPayloadHeaderKeys =
    [
        KafkaConstants.HeaderConstants.ExceptionMessage,
        KafkaConstants.HeaderConstants.RetryExceptionMessage,
        SpringKafkaHeaders.DltExceptionMessage,
        SpringKafkaHeaders.DltExceptionStackTrace,
        SpringKafkaHeaders.DltExceptionFqcn,
        SpringKafkaHeaders.DltExceptionCauseFqcn,
        SpringKafkaHeaders.ExceptionMessage,
        SpringKafkaHeaders.ExceptionStackTrace,
        SpringKafkaHeaders.ExceptionFqcn,
        SpringKafkaHeaders.ExceptionCauseFqcn
    ];

    public static bool IsErrorTopic(string topic) =>
        topic.EndsWith("-Error", StringComparison.OrdinalIgnoreCase);

    public static bool IsRetryTopic(string topic) =>
        topic.EndsWith("-Retry", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True for ResourcesNormalized-Error/Retry. Also accepts the historical misspelling
    /// ResourceNormalized-Error that Automation previously looked for.
    /// </summary>
    public static bool IsResourcesNormalizedFailureTopic(string topic)
    {
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        var isFailure = IsErrorTopic(topic) || IsRetryTopic(topic);
        if (!isFailure)
            return false;

        return topic.Contains("ResourcesNormalized", StringComparison.OrdinalIgnoreCase)
               || topic.Contains("ResourceNormalized", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExceptionPayloadHeader(string headerKey)
    {
        if (string.IsNullOrWhiteSpace(headerKey))
            return false;

        foreach (var known in ExceptionPayloadHeaderKeys)
        {
            if (string.Equals(headerKey, known, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return headerKey.Contains("exception-stacktrace", StringComparison.OrdinalIgnoreCase)
               || headerKey.Contains("exception-message", StringComparison.OrdinalIgnoreCase)
               || headerKey.Contains("exception-cause", StringComparison.OrdinalIgnoreCase);
    }

    public static int HeaderPreviewLength(string headerKey, bool isResourcesNormalizedFailureTopic)
    {
        if (IsExceptionPayloadHeader(headerKey))
            return ExceptionHeaderPreviewLength;

        return isResourcesNormalizedFailureTopic
            ? ResourcesNormalizedHeaderPreviewLength
            : DefaultHeaderPreviewLength;
    }

    public static int ValuePreviewLength(bool isResourcesNormalizedFailureTopic) =>
        isResourcesNormalizedFailureTopic
            ? ResourcesNormalizedValuePreviewLength
            : DefaultValuePreviewLength;

    public static Dictionary<string, string> ReadHeaders(Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (headers == null || headers.Count == 0)
            return result;

        foreach (var header in headers)
        {
            try
            {
                var bytes = header.GetValueBytes();
                result[header.Key] = bytes != null ? Encoding.UTF8.GetString(bytes) : "(null)";
            }
            catch
            {
                // Skip malformed headers
            }
        }

        return result;
    }

    public static string? GetFacilityId(string? messageKey, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var fromHeader)
            && !string.IsNullOrWhiteSpace(fromHeader))
        {
            return fromHeader;
        }

        return TryParseFacilityIdFromKey(messageKey);
    }

    public static string? TryParseFacilityIdFromKey(string? messageKey)
    {
        if (string.IsNullOrWhiteSpace(messageKey) || messageKey == "(null)")
            return null;

        var trimmed = messageKey.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                var json = JObject.Parse(trimmed);
                var id = json["facilityId"]?.ToString() ?? json["FacilityId"]?.ToString();
                if (!string.IsNullOrWhiteSpace(id))
                    return id;
            }
            catch
            {
                // Not JSON; fall through to treating the whole key as a facility id.
            }
        }

        return trimmed;
    }

    /// <summary>
    /// Same scoping rules as the previous key-equality filter, plus JSON ResourceKey /
    /// ReadyForValidation.Key and X-Exception-Facility-Id. Unknown (empty) keys still count
    /// so a missing key cannot hide a dead-letter.
    /// </summary>
    public static bool MatchesFacility(string? messageKey, IReadOnlyDictionary<string, string> headers, string? facilityId)
    {
        if (string.IsNullOrEmpty(facilityId))
            return true;

        if (headers.TryGetValue(KafkaConstants.HeaderConstants.ExceptionFacilityId, out var headerFacility)
            && !string.IsNullOrWhiteSpace(headerFacility))
        {
            return string.Equals(headerFacility, facilityId, StringComparison.Ordinal);
        }

        var fromKey = TryParseFacilityIdFromKey(messageKey);
        if (string.IsNullOrEmpty(fromKey))
            return true;

        return string.Equals(fromKey, facilityId, StringComparison.Ordinal);
    }

    public static string? TrySummarizeResourcesNormalized(string? key, string? value)
    {
        string? facilityId = null;
        string? patientId = null;

        if (!string.IsNullOrWhiteSpace(key) && key.TrimStart().StartsWith('{'))
        {
            try
            {
                var keyJson = JObject.Parse(key);
                facilityId = keyJson["facilityId"]?.ToString() ?? keyJson["FacilityId"]?.ToString();
                patientId = keyJson["patientId"]?.ToString() ?? keyJson["PatientId"]?.ToString();
            }
            catch
            {
                // Key is not JSON; ignore.
            }
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return facilityId == null && patientId == null
                ? null
                : $"facilityId={facilityId ?? "(null)"}, patientId={patientId ?? "(null)"}";
        }

        try
        {
            var json = JObject.Parse(value);
            facilityId ??= json["FacilityId"]?.ToString() ?? json["facilityId"]?.ToString();
            patientId ??= json["PatientId"]?.ToString() ?? json["patientId"]?.ToString();

            var queryType = json["QueryType"]?.ToString() ?? json["queryType"]?.ToString();
            var reportableEvent = json["ReportableEvent"]?.ToString() ?? json["reportableEvent"]?.ToString();
            var cacheType = json["CacheType"]?.ToString() ?? json["cacheType"]?.ToString();
            var cacheKey = json["CacheKey"]?.ToString() ?? json["cacheKey"]?.ToString();

            var resourceToken = json["Resource"] ?? json["resource"];
            var resourceType = resourceToken?["resourceType"]?.ToString();
            var resourceId = resourceToken?["id"]?.ToString();

            var scheduledReports = json["ScheduledReports"] ?? json["scheduledReports"];
            var scheduledCount = scheduledReports is JArray reports ? reports.Count : 0;

            return $"facilityId={facilityId ?? "(null)"}, patientId={patientId ?? "(null)"}, queryType={queryType ?? "(null)"}, reportableEvent={reportableEvent ?? "(null)"}, cacheType={cacheType ?? "(null)"}, cacheKey={cacheKey ?? "(null)"}, resourceType={resourceType ?? "(null)"}, resourceId={resourceId ?? "(null)"}, scheduledReports={scheduledCount}";
        }
        catch
        {
            return facilityId == null && patientId == null
                ? null
                : $"facilityId={facilityId ?? "(null)"}, patientId={patientId ?? "(null)"}";
        }
    }
}
