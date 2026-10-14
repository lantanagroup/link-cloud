using LantanaGroup.Link.Shared.KafkaAdmin.Migration;

namespace LantanaGroup.Link.Operations.Application.KafkaOps;

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
