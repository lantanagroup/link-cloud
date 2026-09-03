using System.Text;
using Confluent.Kafka;
using LantanaGroup.Link.Shared.Settings;

namespace LantanaGroup.Link.Shared.Application.Utilities;

public class KafkaHeaderHelper
{
    private static string? GetHeaderByKey(Headers headers, string key)
    {
        if (headers.TryGetLastBytes(key, out var value))
            return Encoding.UTF8.GetString(value);
        return null;
    }

    public static string? GetExceptionFacilityId(Headers headers) => GetHeaderByKey(headers, KafkaConstants.HeaderConstants.ExceptionFacilityId);

    public static string? GetCorrelationId(Headers headers) => GetHeaderByKey(headers, KafkaConstants.HeaderConstants.CorrelationId);

    public static string? GetMetricsMode(Headers? headers) =>
        headers == null ? null : GetHeaderByKey(headers, KafkaConstants.HeaderConstants.MetricsMode);

    public static void SetMetricsMode(Headers headers, string mode)
    {
        ArgumentNullException.ThrowIfNull(headers);
        if (string.IsNullOrWhiteSpace(mode))
        {
            return;
        }

        headers.Remove(KafkaConstants.HeaderConstants.MetricsMode);
        headers.Add(KafkaConstants.HeaderConstants.MetricsMode, Encoding.UTF8.GetBytes(mode));
    }

    /// <summary>
    /// Copies X-Metrics-Mode from <paramref name="source"/> onto <paramref name="destination"/> if present.
    /// Missing header is lightweight (destination unchanged).
    /// </summary>
    public static void CopyMetricsMode(Headers? source, Headers destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var mode = GetMetricsMode(source);
        if (!string.IsNullOrEmpty(mode))
        {
            SetMetricsMode(destination, mode);
        }
    }

    public static bool IsPerformanceMode(Headers? headers)
    {
        var mode = GetMetricsMode(headers);
        return IsPerformanceMode(mode);
    }

    public static bool IsPerformanceMode(string? mode) =>
        string.Equals(mode, "performance", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sets X-Metrics-Mode=performance when <paramref name="mode"/> is performance.
    /// Missing/unknown is lightweight (header omitted).
    /// </summary>
    public static void ApplyIfPerformance(Headers headers, string? mode)
    {
        if (IsPerformanceMode(mode))
            SetMetricsMode(headers, "performance");
    }
}