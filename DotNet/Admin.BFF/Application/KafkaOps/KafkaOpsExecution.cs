namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public static class KafkaOpsExecution
{
    public static bool Allows(bool canManageTopics, bool canScale, KafkaChangeKind kind) =>
        kind is KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily or KafkaChangeKind.ReplicationFactor or KafkaChangeKind.Produce
            ? canManageTopics
            : canScale;
}
