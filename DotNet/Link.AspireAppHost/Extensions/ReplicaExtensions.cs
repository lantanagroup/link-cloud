using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// How many copies of each service run, from the <c>Replicas</c> section of this host's settings, keyed by
/// resource name (<c>Replicas:validation</c>, <c>Replicas:normalization</c>). One when a service is not listed.
/// </summary>
/// <remarks>
/// It mimics a deployed service scaled out: every copy runs the same code with the same settings, and the
/// copies share the service's Kafka consumer groups, so a topic's partitions are split between them. What a
/// copy does at startup (migrations, scheduled jobs) every copy does, as it would deployed; a service that
/// misbehaves with two copies here would misbehave with two deployed.
/// </remarks>
internal static class ReplicaExtensions
{
    public const string SectionName = "Replicas";

    /// <summary>
    /// Every topic in <c>topics.txt</c> has this many partitions. A consumer group uses at most one copy per
    /// partition, so copies beyond it sit idle.
    /// </summary>
    private const int TopicPartitions = 3;

    /// <summary>The number of copies configured for <paramref name="name"/>.</summary>
    /// <exception cref="InvalidOperationException">The setting is not a whole number of at least one.</exception>
    public static int ReplicaCount(this IDistributedApplicationBuilder builder, string name)
    {
        var value = builder.Configuration[$"{SectionName}:{name}"];
        if (string.IsNullOrWhiteSpace(value))
            return 1;
        if (!int.TryParse(value, out var count) || count < 1)
            throw new InvalidOperationException($"{SectionName}:{name} is \"{value}\"; it must be a whole number of at least 1.");

        if (count > TopicPartitions)
        {
            builder.Eventing.Subscribe<BeforeStartEvent>((start, _) =>
            {
                start.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ReplicaExtensions).FullName!)
                    .LogWarning("{Setting} is {Count}, but each topic has {Partitions} partitions, and a consumer group gives each partition to one copy: the copies beyond {Partitions} get none and sit idle.",
                        $"{SectionName}:{name}", count, TopicPartitions, TopicPartitions);
                return Task.CompletedTask;
            });
        }

        return count;
    }
}
