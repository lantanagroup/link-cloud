using LantanaGroup.Link.Shared.Application.Models.Configs;
using System.Collections.Concurrent;
using System.Text.Json;
using LantanaGroup.Link.Shared.Application.Interfaces;

namespace LantanaGroup.Link.Operations.Application.KafkaOps;

/// <summary>
/// Where change requests live. Every replica reads and writes the same store, and a save
/// that lost a race to another replica is refused instead of overwriting it.
/// </summary>
public interface IChangeRequestStore
{
    bool Durable { get; }
    Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChangeRequestRecord>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Inserts a new record or updates one whose StoreVersion still matches what is stored.
    /// On success the record's StoreVersion is advanced. A mismatch throws <see cref="ChangeRequestConflictException"/>.
    /// </summary>
    Task SaveAsync(ChangeRequestRecord record, CancellationToken cancellationToken);
}

public sealed class ChangeRequestConflictException : KafkaOpsRejectedException
{
    public const string Detail = "This change request was changed by another session. Reload it and try again.";

    public ChangeRequestConflictException() : base(Detail) { }
}

public static class ChangeRequestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Write(ChangeRequestRecord record) => JsonSerializer.Serialize(record, Options);

    public static ChangeRequestRecord? Read(string json) => JsonSerializer.Deserialize<ChangeRequestRecord>(json, Options);
}

/// <summary>For tests and a single local process. Nothing survives a restart.</summary>
public sealed class InMemoryChangeRequestStore : IChangeRequestStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, (long Version, string Json)> _items = [];
    private readonly List<Guid> _order = [];

    public bool Durable => false;

    public Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult(_items.TryGetValue(id, out var item) ? Hydrate(item) : null);
    }

    public Task<IReadOnlyList<ChangeRequestRecord>> ListAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            IReadOnlyList<ChangeRequestRecord> list = _order.Select(id => Hydrate(_items[id])!).ToList();
            return Task.FromResult(list);
        }
    }

    public Task SaveAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_items.TryGetValue(record.Id, out var current))
            {
                if (current.Version != record.StoreVersion)
                    throw new ChangeRequestConflictException();
            }
            else
            {
                if (record.StoreVersion != 0)
                    throw new ChangeRequestConflictException();
                _order.Add(record.Id);
            }

            record.StoreVersion++;
            _items[record.Id] = (record.StoreVersion, ChangeRequestJson.Write(record));
        }

        return Task.CompletedTask;
    }

    private static ChangeRequestRecord? Hydrate((long Version, string Json) item)
    {
        var record = ChangeRequestJson.Read(item.Json);
        if (record is not null)
            record.StoreVersion = item.Version;
        return record;
    }
}

/// <summary>
/// The original cache layout (24 hour keys and an index list). Used only when no store is registered,
/// so existing callers that build the service by hand keep working. It is not durable and not race safe.
/// </summary>
public sealed class CacheChangeRequestStore : IChangeRequestStore
{
    private readonly ICacheService _cache;
    private readonly string _environment;

    public CacheChangeRequestStore(ICacheService cache, string environment)
    {
        _cache = cache;
        _environment = environment;
    }

    public bool Durable => false;

    public async Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await _cache.GetAsync<ChangeRequestRecord>(KafkaOpsService.RequestStorageKey(_environment, id), cancellationToken);

    public async Task<IReadOnlyList<ChangeRequestRecord>> ListAsync(CancellationToken cancellationToken)
    {
        var records = new List<ChangeRequestRecord>();
        foreach (var id in await IdsAsync(cancellationToken))
        {
            var record = await GetAsync(id, cancellationToken);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }

    public async Task SaveAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        record.StoreVersion++;
        await _cache.SetAsync(KafkaOpsService.RequestStorageKey(_environment, record.Id), record, TimeSpan.FromHours(24), ExpirationType.Absolute, cancellationToken);
        var ids = await IdsAsync(cancellationToken);
        if (!ids.Contains(record.Id))
            ids.Add(record.Id);
        await _cache.SetAsync(KafkaOpsService.IndexKey(_environment), ids, TimeSpan.FromHours(24), ExpirationType.Absolute, cancellationToken);
    }

    private async Task<List<Guid>> IdsAsync(CancellationToken cancellationToken) =>
        await _cache.GetAsync<List<Guid>>(KafkaOpsService.IndexKey(_environment), cancellationToken) ?? [];
}
