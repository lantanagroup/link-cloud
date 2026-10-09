using Confluent.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

internal readonly record struct MetadataLogLeader(bool Present, int Leader);

/// <summary>
/// KRaft brokers fill DescribeCluster.Controller with a random live broker.
/// The active controller is the leader of the metadata log.
/// </summary>
internal static class KafkaControllerId
{
    public const string MetadataLogTopic = "__cluster_metadata";

    public static MetadataLogLeader Read(Metadata? metadata)
    {
        var topic = metadata?.Topics?.FirstOrDefault(item =>
            string.Equals(item.Topic, MetadataLogTopic, StringComparison.Ordinal));
        if (topic is null)
            return new MetadataLogLeader(false, -1);
        if (topic.Error.IsError && topic.Error.Code == ErrorCode.UnknownTopicOrPart)
            return new MetadataLogLeader(false, -1);

        var leader = topic.Partitions?.FirstOrDefault(partition => partition.PartitionId == 0)?.Leader ?? -1;
        return new MetadataLogLeader(true, leader);
    }

    public static int? Resolve(
        int? describedControllerId,
        MetadataLogLeader logLeader,
        bool rolesKnown,
        IReadOnlyCollection<int> controllerEligibleBrokerIds)
    {
        if (logLeader.Present && logLeader.Leader >= 0)
            return logLeader.Leader;

        // Dedicated controllers are not in the broker list. The described id is a random broker.
        if (rolesKnown && controllerEligibleBrokerIds.Count == 0)
            return null;

        if (!logLeader.Present && describedControllerId is int id && id >= 0)
            return id;

        return null;
    }
}
