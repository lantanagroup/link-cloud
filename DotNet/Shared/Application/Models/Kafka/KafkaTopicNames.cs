namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Retry and redrive topic names. A retry is delivered only to the service that failed.
/// The shared main topic is never the retry destination.
/// </summary>
public static class KafkaTopicNames
{
    public const string RetryMarker = "-Retry-";
    public const string RedriveMarker = "-Redrive-";

    public static string Retry(string mainTopic, string serviceName)
    {
        Require(mainTopic, serviceName);
        return mainTopic + RetryMarker + serviceName;
    }

    public static string Redrive(string mainTopic, string serviceName)
    {
        Require(mainTopic, serviceName);
        return mainTopic + RedriveMarker + serviceName;
    }

    public static string Error(string mainTopic)
    {
        if (string.IsNullOrWhiteSpace(mainTopic))
        {
            throw new ArgumentException("Topic is required.", nameof(mainTopic));
        }

        return mainTopic + "-Error";
    }

    public static string[] Subscription(string mainTopic, string serviceName)
    {
        return [mainTopic, Redrive(mainTopic, serviceName)];
    }

    public static bool TryMainFromRetry(string? topic, out string mainTopic, out string serviceName)
    {
        return TrySplit(topic, RetryMarker, out mainTopic, out serviceName);
    }

    public static bool TryMainFromRedrive(string? topic, out string mainTopic, out string serviceName)
    {
        return TrySplit(topic, RedriveMarker, out mainTopic, out serviceName);
    }

    private static bool TrySplit(string? topic, string marker, out string mainTopic, out string serviceName)
    {
        mainTopic = string.Empty;
        serviceName = string.Empty;
        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        var index = topic.LastIndexOf(marker, StringComparison.Ordinal);
        if (index <= 0 || index + marker.Length >= topic.Length)
        {
            return false;
        }

        mainTopic = topic[..index];
        serviceName = topic[(index + marker.Length)..];
        return !string.IsNullOrWhiteSpace(mainTopic) && !string.IsNullOrWhiteSpace(serviceName);
    }

    private static void Require(string mainTopic, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(mainTopic))
        {
            throw new ArgumentException("Topic is required.", nameof(mainTopic));
        }

        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("Service name is required.", nameof(serviceName));
        }
    }
}
