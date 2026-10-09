namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

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

public sealed class MigrationHoldRegistry : IMigrationHoldRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Guid> _held = new(StringComparer.Ordinal);
    private readonly IMigrationStore? _store;

    public MigrationHoldRegistry(IMigrationStore? store = null)
    {
        _store = store;
    }

    public static bool Armed(MigrationStep step) =>
        step is MigrationStep.A4 or MigrationStep.B1 or MigrationStep.B2 or MigrationStep.B3 or MigrationStep.B4
            or MigrationStep.B5 or MigrationStep.B6 or MigrationStep.B7 or MigrationStep.H1
            or MigrationStep.C1 or MigrationStep.C2 or MigrationStep.C3 or MigrationStep.C4 or MigrationStep.C5
            or MigrationStep.D1 or MigrationStep.NeedsAttention;

    public bool IsHeld(string topic)
    {
        lock (_gate)
            return _held.ContainsKey(topic);
    }

    public async Task<bool> IsHeldAsync(string topic, CancellationToken cancellationToken)
    {
        if (IsHeld(topic))
            return true;
        if (_store is null)
            return false;
        var records = await _store.ListAsync(cancellationToken);
        return records.Any(record => string.Equals(record.Topic, topic, StringComparison.Ordinal) && Armed(record.Step));
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

    public async Task<IReadOnlyCollection<string>> HeldTopicsAsync(CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(HeldTopics(), StringComparer.Ordinal);
        if (_store is null)
            return names;
        foreach (var record in await _store.ListAsync(cancellationToken))
        {
            if (Armed(record.Step))
                names.Add(record.Topic);
        }

        return names;
    }
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
