namespace LantanaGroup.Link.Shared.Application.Models.Kafka;

public sealed record KafkaCodeSite(string Workload, string Path, bool ControlPlane = false);

/// <summary>
/// Producers, consumers, retry siblings, and slice-1 eligibility for one main topic.
/// Paths are repository-relative and use forward slashes.
/// </summary>
public sealed class KafkaTopicFamily
{
    public string Topic { get; init; } = "";
    public bool Slice1Eligible { get; init; }
    public string IneligibleReason { get; init; } = "";
    public bool JavaPinnedSiblings { get; init; }
    public IReadOnlyList<string> RetryServices { get; init; } = [];
    public IReadOnlyList<string> RedriveOnlyServices { get; init; } = [];
    public IReadOnlyList<KafkaCodeSite> Producers { get; init; } = [];
    public IReadOnlyList<KafkaCodeSite> Consumers { get; init; } = [];

    public IReadOnlyList<string> ProducerWorkloads =>
        Producers.Where(site => !site.ControlPlane).Select(site => site.Workload).Distinct(StringComparer.Ordinal).ToList();

    public IReadOnlyList<string> ConsumerWorkloads =>
        Consumers.Where(site => !site.ControlPlane).Select(site => site.Workload).Distinct(StringComparer.Ordinal).ToList();

    public IReadOnlyList<string> StopSet
    {
        get
        {
            var names = new List<string>();
            foreach (var name in ProducerWorkloads.Concat(ConsumerWorkloads))
            {
                if (!names.Contains(name, StringComparer.Ordinal))
                    names.Add(name);
            }

            return names;
        }
    }

    public bool HasControlPlaneProducer => Producers.Any(site => site.ControlPlane);
}
