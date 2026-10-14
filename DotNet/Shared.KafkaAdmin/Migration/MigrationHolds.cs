namespace LantanaGroup.Link.Shared.KafkaAdmin.Migration;

/// <summary>
/// Topics a Kafka topic migration holds. Producers and test consumers outside the Operations service ask this before they touch a topic.
/// </summary>
public interface IMigrationHoldRegistry
{
    bool IsHeld(string topic);
    Task<bool> IsHeldAsync(string topic, CancellationToken cancellationToken);
    Guid? MigrationFor(string topic);
    void Set(string topic, Guid migrationId, bool held);
    IReadOnlyCollection<string> HeldTopics();
    Task<IReadOnlyCollection<string>> HeldTopicsAsync(CancellationToken cancellationToken);
}

public sealed class MigrationHoldException : Exception
{
    public MigrationHoldException(string topic, Guid? migrationId)
        : base("Topic " + topic + " is held" + (migrationId is { } id ? " by migration " + id.ToString("D") : "") + ".")
    {
        Topic = topic;
        MigrationId = migrationId;
    }

    public string Topic { get; }
    public Guid? MigrationId { get; }
}

public static class MigrationHoldGuard
{
    public static void RefuseIfHeld(IMigrationHoldRegistry? holds, string topic)
    {
        if (holds is not null && holds.IsHeld(topic))
            throw new MigrationHoldException(topic, holds.MigrationFor(topic));
    }

    public static async Task RefuseIfHeldAsync(IMigrationHoldRegistry? holds, string topic, CancellationToken cancellationToken)
    {
        if (holds is not null && await holds.IsHeldAsync(topic, cancellationToken))
            throw new MigrationHoldException(topic, holds.MigrationFor(topic));
    }
}
