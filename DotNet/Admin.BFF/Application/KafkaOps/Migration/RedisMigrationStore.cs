using Medallion.Threading.Redis;
using StackExchange.Redis;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class RedisMigrationStore : IMigrationStore
{
    private readonly IDatabase _database;
    private readonly string _prefix;
    private readonly string _executorFence;

    public RedisMigrationStore(IDatabase database, string environment)
    {
        _database = database;
        _prefix = "kafka-ops:" + environment + ":migration:";
        _executorFence = "kafka-ops:" + environment + ":executor:fence";
    }

    public bool Durable => true;

    public async Task SaveAsync(MigrationRecord record, long fence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        record.Fence = fence;
        var json = System.Text.Json.JsonSerializer.Serialize(record);
        var script = """
            local current = redis.call('GET', KEYS[2])
            local token = tonumber(ARGV[1])
            if current and token < tonumber(current) then
              local counter = tonumber(redis.call('GET', KEYS[4])) or 0
              if counter < tonumber(current) then
                redis.call('SET', KEYS[4], current)
              end
              return 0
            end
            redis.call('SET', KEYS[1], ARGV[2])
            redis.call('SET', KEYS[2], ARGV[1])
            redis.call('SADD', KEYS[3], ARGV[3])
            return 1
            """;
        var saved = (int)await _database.ScriptEvaluateAsync(
            script,
            [_prefix + record.Id.ToString("N"), _prefix + record.Id.ToString("N") + ":fence", _prefix + "ids", _executorFence],
            [fence, json, record.Id.ToString("N")]);
        if (saved == 0)
            throw new StaleFenceException();
    }

    public async Task<MigrationRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await _database.StringGetAsync(_prefix + id.ToString("N"));
        if (json.IsNullOrEmpty)
            return null;
        return System.Text.Json.JsonSerializer.Deserialize<MigrationRecord>(json!);
    }

    public async Task<IReadOnlyList<MigrationRecord>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ids = await _database.SetMembersAsync(_prefix + "ids");
        var records = new List<MigrationRecord>();
        foreach (var id in ids)
        {
            if (!Guid.TryParseExact(id, "N", out var parsed))
                continue;
            var record = await GetAsync(parsed, cancellationToken);
            if (record is not null)
                records.Add(record);
        }

        return records;
    }
}

public sealed class RedisKafkaOpsLease : IKafkaOpsLease, IDisposable
{
    private readonly IDatabase _database;
    private readonly RedisDistributedLock _lock;
    private readonly RedisKey _fenceKey;
    private IDisposable? _held;

    public RedisKafkaOpsLease(IDatabase database, string environment)
    {
        _database = database;
        _fenceKey = "kafka-ops:" + environment + ":executor:fence";
        _lock = new RedisDistributedLock(
            "kafka-ops:" + environment + ":executor",
            database,
            options => options.Expiry(TimeSpan.FromSeconds(30)).ExtensionCadence(TimeSpan.FromSeconds(5)));
    }

    public async Task<KafkaOpsLeaseHold> AcquireAsync(CancellationToken cancellationToken)
    {
        var handle = await _lock.TryAcquireAsync(TimeSpan.Zero, cancellationToken);
        if (handle is null)
            throw new LeaseHeldException();
        _held = handle;
        var token = await _database.StringIncrementAsync(_fenceKey);
        return new KafkaOpsLeaseHold(token, () =>
        {
            handle.Dispose();
            if (ReferenceEquals(_held, handle))
                _held = null;
        });
    }

    public void Dispose()
    {
        _held?.Dispose();
        _held = null;
    }
}
