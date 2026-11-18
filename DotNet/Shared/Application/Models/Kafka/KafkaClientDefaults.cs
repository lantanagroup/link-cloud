using System.Net;
using Confluent.Kafka;

namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

/// <summary>
/// Shared producer settings. Every .NET producer is built through
/// <see cref="ApplyProducer"/> so the partitioner cannot be omitted.
/// </summary>
public static class KafkaClientDefaults
{
    public const int MetadataRefreshIntervalMs = 30_000;

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
