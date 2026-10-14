using System.Net;
using Confluent.Kafka;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Shared producer and consumer settings. Every .NET producer is built through
/// <see cref="ApplyProducer"/> so the partitioner cannot be omitted.
/// </summary>
public static class KafkaClientDefaults
{
    public const int MetadataRefreshIntervalMs = 30_000;
    public const int ConsumerConfigVersion = 1;
    public const int QueuedMaxMessagesKbytes = 102_400;

    public static void ApplyProducer(ProducerConfig config, string? serviceClientId = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        config.Partitioner = Partitioner.Murmur2Random;
        config.Acks = Acks.All;
        config.EnableIdempotence = true;
        config.MaxInFlight ??= 5;
        if (config.MessageSendMaxRetries is null or 0)
        {
            config.MessageSendMaxRetries = int.MaxValue;
        }

        config.TopicMetadataRefreshIntervalMs ??= MetadataRefreshIntervalMs;
        config.ClientId = BuildClientId(serviceClientId ?? config.ClientId, "p");
    }

    public static void ApplyConsumer(ConsumerConfig config, string? serviceClientId = null, bool staticMembership = false, string? staticMemberName = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.AutoOffsetReset is null)
        {
            config.AutoOffsetReset = AutoOffsetReset.Earliest;
        }

        config.PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky;
        config.TopicMetadataRefreshIntervalMs ??= MetadataRefreshIntervalMs;
        config.QueuedMaxMessagesKbytes ??= QueuedMaxMessagesKbytes;
        config.ClientId = BuildClientId(serviceClientId ?? config.ClientId, "c" + ConsumerConfigVersion.ToString());
        if (staticMembership && string.IsNullOrWhiteSpace(config.GroupInstanceId))
        {
            var member = string.IsNullOrWhiteSpace(staticMemberName) ? "static" : "static-" + staticMemberName.Trim();
            config.GroupInstanceId = FitGroupInstanceId(BuildClientId(serviceClientId ?? config.ClientId, member), member);
        }
    }

    private static string FitGroupInstanceId(string instanceId, string member)
    {
        const int maxLength = 249;
        if (instanceId.Length <= maxLength)
        {
            return instanceId;
        }

        var suffix = "-" + member;
        if (suffix.Length >= maxLength)
        {
            return suffix.Substring(suffix.Length - maxLength, maxLength);
        }

        return instanceId.Substring(0, maxLength - suffix.Length) + suffix;
    }

    public static string BuildClientId(string? serviceClientId, string role)
    {
        var service = string.IsNullOrWhiteSpace(serviceClientId) ? "link" : serviceClientId.Trim();
        var host = Environment.GetEnvironmentVariable("HOSTNAME");
        if (string.IsNullOrWhiteSpace(host))
        {
            host = Dns.GetHostName();
        }

        return service + "-" + host + "-" + role;
    }
}
