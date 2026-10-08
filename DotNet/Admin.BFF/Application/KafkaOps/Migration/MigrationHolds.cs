namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IMigrationHoldRegistry
{
    bool IsHeld(string topic);
    Guid? MigrationFor(string topic);
    void Set(string topic, Guid migrationId, bool held);
    IReadOnlyCollection<string> HeldTopics();
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

public sealed class MigrationHoldRegistry : IMigrationHoldRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Guid> _held = new(StringComparer.Ordinal);

    public bool IsHeld(string topic)
    {
        lock (_gate)
            return _held.ContainsKey(topic);
    }

    public Guid? MigrationFor(string topic)
    {
        lock (_gate)
            return _held.TryGetValue(topic, out var id) ? id : null;
    }

    public void Set(string topic, Guid migrationId, bool held)
    {
        lock (_gate)
        {
            if (held)
                _held[topic] = migrationId;
            else if (_held.TryGetValue(topic, out var current) && current == migrationId)
                _held.Remove(topic);
        }
    }

    public IReadOnlyCollection<string> HeldTopics()
    {
        lock (_gate)
            return _held.Keys.ToList();
    }
}

public static class MigrationHoldGuard
{
    public static void RefuseIfHeld(IMigrationHoldRegistry? holds, string topic)
    {
        if (holds is not null && holds.IsHeld(topic))
            throw new MigrationHoldException(topic, holds.MigrationFor(topic));
    }
}
