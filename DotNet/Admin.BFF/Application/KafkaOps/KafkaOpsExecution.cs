namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public static class KafkaOpsExecution
{
    public static bool Allows(bool canManageTopics, bool canScale, KafkaChangeKind kind) =>
        kind is KafkaChangeKind.PartitionIncrease or KafkaChangeKind.CompleteTopicFamily ? canManageTopics : canScale;
}
