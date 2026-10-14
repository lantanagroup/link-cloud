using LantanaGroup.Link.Operations.Application.KafkaOps;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Operations.Persistence;

/// <summary>
/// Change requests in the Operations database. Updates are conditional on the version that was read,
/// so two replicas acting on the same request cannot both win.
/// </summary>
public sealed class SqlChangeRequestStore : IChangeRequestStore
{
    public const int HistoryLimit = 500;

    private readonly IDbContextFactory<OperationsDbContext> _factory;
    private readonly string _environment;
    private readonly int _historyDays;

    public SqlChangeRequestStore(IDbContextFactory<OperationsDbContext> factory, string environment, int historyDays)
    {
        _factory = factory;
        _environment = environment;
        _historyDays = Math.Max(1, historyDays);
    }

    public bool Durable => true;

    public async Task<ChangeRequestRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.ChangeRequests.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.Environment == _environment, cancellationToken);
        return row is null ? null : Hydrate(row);
    }

    public async Task<IReadOnlyList<ChangeRequestRecord>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-_historyDays).ToUnixTimeMilliseconds();
        var open = await db.ChangeRequests.AsNoTracking()
            .Where(item => item.Environment == _environment && item.IsOpen)
            .ToListAsync(cancellationToken);
        var closed = await db.ChangeRequests.AsNoTracking()
            .Where(item => item.Environment == _environment && !item.IsOpen && item.CreatedUnixMs >= cutoff)
            .OrderByDescending(item => item.CreatedUnixMs)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);
        return open.Concat(closed)
            .OrderBy(item => item.CreatedUnixMs)
            .Select(Hydrate)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }

    public async Task SaveAsync(ChangeRequestRecord record, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var expected = record.StoreVersion;
        var next = expected + 1;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var payload = ChangeRequestJson.Write(record);
        var kind = record.Kind.ToString();
        var status = record.Status.ToString();
        var open = IsOpen(record.Status);

        if (expected == 0)
        {
            db.ChangeRequests.Add(new ChangeRequestEntity
            {
                Id = record.Id,
                Environment = _environment,
                Kind = kind,
                Status = status,
                Topic = record.Topic,
                IsOpen = open,
                CreatedUnixMs = record.CreatedUtc == default ? now : record.CreatedUtc.ToUnixTimeMilliseconds(),
                UpdatedUnixMs = now,
                Version = next,
                Payload = payload
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new ChangeRequestConflictException();
            }
        }
        else
        {
            var changed = await db.ChangeRequests
                .Where(item => item.Id == record.Id && item.Environment == _environment && item.Version == expected)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(item => item.Kind, kind)
                    .SetProperty(item => item.Status, status)
                    .SetProperty(item => item.Topic, record.Topic)
                    .SetProperty(item => item.IsOpen, open)
                    .SetProperty(item => item.UpdatedUnixMs, now)
                    .SetProperty(item => item.Version, next)
                    .SetProperty(item => item.Payload, payload), cancellationToken);
            if (changed != 1)
                throw new ChangeRequestConflictException();
        }

        record.StoreVersion = next;
    }

    /// <summary>Open matches the console's in-flight rule, plus TimedOut, which still owns a reassignment until it is cleared.</summary>
    public static bool IsOpen(KafkaChangeStatus status) =>
        status is KafkaChangeStatus.Pending or KafkaChangeStatus.Approved or KafkaChangeStatus.Executing
            or KafkaChangeStatus.Converging or KafkaChangeStatus.Verifying or KafkaChangeStatus.TimedOut;

    private static ChangeRequestRecord? Hydrate(ChangeRequestEntity row)
    {
        var record = ChangeRequestJson.Read(row.Payload);
        if (record is not null)
            record.StoreVersion = row.Version;
        return record;
    }
}
