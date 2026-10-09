namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public interface IMigrationStore
{
    bool Durable { get; }
    Task SaveAsync(MigrationRecord record, long fence, CancellationToken cancellationToken);
    Task<MigrationRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<MigrationRecord>> ListAsync(CancellationToken cancellationToken);
}

public interface IKafkaOpsLease
{
    Task<KafkaOpsLeaseHold> AcquireAsync(CancellationToken cancellationToken);
}

public sealed class KafkaOpsLeaseHold : IAsyncDisposable
{
    private readonly Action _release;
    public long Fence { get; }

    public KafkaOpsLeaseHold(long fence, Action release)
    {
        Fence = fence;
        _release = release;
    }

    public ValueTask DisposeAsync()
    {
        _release();
        return ValueTask.CompletedTask;
    }
}

public sealed class StaleFenceException : Exception
{
    public StaleFenceException() : base("The executor fencing token is stale.") { }
}

public sealed class LeaseHeldException : KafkaOpsRejectedException
{
    public LeaseHeldException() : base("The executor lease is held.") { }
}

public sealed class InMemoryMigrationStore : IMigrationStore
{
    private readonly Dictionary<Guid, (long Fence, string Json)> _items = [];

    public bool Durable => false;

    public Task SaveAsync(MigrationRecord record, long fence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_items.TryGetValue(record.Id, out var existing) && fence < existing.Fence)
            throw new StaleFenceException();
        record.Fence = fence;
        _items[record.Id] = (fence, System.Text.Json.JsonSerializer.Serialize(record));
        return Task.CompletedTask;
    }

    public Task<MigrationRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_items.TryGetValue(id, out var existing))
            return Task.FromResult<MigrationRecord?>(null);
        return Task.FromResult(System.Text.Json.JsonSerializer.Deserialize<MigrationRecord>(existing.Json));
    }

    public Task<IReadOnlyList<MigrationRecord>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MigrationRecord> records = _items.Values
            .Select(item => System.Text.Json.JsonSerializer.Deserialize<MigrationRecord>(item.Json)!)
            .ToList();
        return Task.FromResult(records);
    }
}

public sealed class InMemoryKafkaOpsLease : IKafkaOpsLease, IDisposable
{
    private int _held;
    private long _token;

    public Task<KafkaOpsLeaseHold> AcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _held, 1, 0) != 0)
            throw new LeaseHeldException();
        var token = Interlocked.Increment(ref _token);
        return Task.FromResult(new KafkaOpsLeaseHold(token, () => Interlocked.Exchange(ref _held, 0)));
    }

    public void Dispose() => Interlocked.Exchange(ref _held, 0);
}
