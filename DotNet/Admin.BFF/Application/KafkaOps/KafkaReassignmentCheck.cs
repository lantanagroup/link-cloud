namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

/// <summary>
/// The reassignment-in-flight answer: Kafka's ListPartitionReassignments, plus the provider's own rebalance status only
/// when a provider that runs rebalances outside Kafka's list is configured (Strimzi KafkaRebalance resources).
/// Open change requests are added by the caller. Unknown from Kafka, or from a configured provider, means unknown, and callers refuse.
/// </summary>
public static class KafkaReassignmentCheck
{
    public static async Task<ReassignmentListing> ListAsync(IKafkaBrokerGateway broker, IKafkaInfraProvider infra, CancellationToken cancellationToken)
    {
        var listing = await broker.ListInFlightReassignmentsAsync(cancellationToken);
        if (!listing.Known || !ReportsOwnRebalances(infra))
            return listing;

        var provider = await infra.ListInFlightReassignmentsAsync(cancellationToken);
        if (!provider.Known)
            return new ReassignmentListing { Error = "The " + infra.Name + " provider did not report its rebalances. " + provider.Error };
        return new ReassignmentListing
        {
            Known = true,
            Topics = listing.Topics.Union(provider.Topics, StringComparer.Ordinal).ToList(),
            Partitions = listing.Partitions.Union(provider.Partitions, StringComparer.Ordinal).ToList()
        };
    }

    public static bool ReportsOwnRebalances(IKafkaInfraProvider infra) =>
        infra.Enabled && infra.Name.Equals("Strimzi", StringComparison.OrdinalIgnoreCase);

    public static string UnknownDetail(ReassignmentListing listing)
    {
        var detail = "Cannot confirm no reassignment is in flight: Kafka did not answer the partition reassignment listing, so this change was not started.";
        return string.IsNullOrWhiteSpace(listing.Error) ? detail : detail + " " + listing.Error.Trim();
    }
}
