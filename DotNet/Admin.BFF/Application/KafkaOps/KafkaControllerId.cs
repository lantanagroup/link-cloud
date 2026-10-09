using Confluent.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

internal readonly record struct MetadataLogLeader(bool Present, int Leader);

internal readonly record struct ControllerResolution(int? Id, string UnavailableReason);

/// <summary>
/// KRaft brokers fill DescribeCluster.Controller with a random live broker.
/// The active controller is the leader of the metadata log.
/// </summary>
internal static class KafkaControllerId
{
    public const string MetadataLogTopic = "__cluster_metadata";

    public const string QuorumNotExposedReason =
        "The KRaft quorum leader is a separate node and is not exposed to this client.";

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

    public static ControllerResolution Resolve(
        int? describedControllerId,
        MetadataLogLeader logLeader,
        bool rolesKnown,
        IReadOnlyCollection<int> controllerEligibleBrokerIds)
    {
        if (logLeader.Present && logLeader.Leader >= 0)
            return new ControllerResolution(logLeader.Leader, "");

        // Dedicated controllers are not in the broker list. DescribeCluster.Controller is a
        // random live broker, so it is not shown as the controller.
        // TODO: when the Kafka admin client exposes DescribeMetadataQuorum, read the quorum
        // leader here and return that id. Confluent.Kafka 2.16.0 does not expose it.
        if (rolesKnown && controllerEligibleBrokerIds.Count == 0)
            return new ControllerResolution(null, QuorumNotExposedReason);

        if (!logLeader.Present && describedControllerId is int id && id >= 0)
            return new ControllerResolution(id, "");

        return new ControllerResolution(null, "");
    }
}
