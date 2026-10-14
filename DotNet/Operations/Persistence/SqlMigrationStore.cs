using System.Text.Json;
using LantanaGroup.Link.Operations.Application.KafkaOps;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Operations.Persistence;

/// <summary>Topic migrations in the Operations database. A save carrying an older fence than the stored one is refused.</summary>
public sealed class SqlMigrationStore : IMigrationStore
{
    private readonly IDbContextFactory<OperationsDbContext> _factory;
    private readonly string _environment;

    public SqlMigrationStore(IDbContextFactory<OperationsDbContext> factory, string environment)
    {
        _factory = factory;
        _environment = environment;
    }

    public bool Durable => true;

    public async Task SaveAsync(MigrationRecord record, long fence, CancellationToken cancellationToken)
    {
        record.Fence = fence;
        var payload = JsonSerializer.Serialize(record);
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var changed = await db.Migrations
                .Where(item => item.Id == record.Id && item.Fence <= fence)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(item => item.Fence, fence)
                    .SetProperty(item => item.Topic, record.Topic)
                    .SetProperty(item => item.UpdatedUnixMs, now)
                    .SetProperty(item => item.Payload, payload), cancellationToken);
            if (changed == 1)
                return;
            if (await db.Migrations.AnyAsync(item => item.Id == record.Id, cancellationToken))
                throw new StaleFenceException();

            db.Migrations.Add(new MigrationEntity
            {
                Id = record.Id,
                Environment = _environment,
                Topic = record.Topic,
                Fence = fence,
                UpdatedUnixMs = now,
                Payload = payload
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException)
            {
                // Inserted by another writer between the two statements. Go round once more and let the fence decide.
            }
        }

        throw new StaleFenceException();
    }

    public async Task<MigrationRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var row = await db.Migrations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.Environment == _environment, cancellationToken);
        return row is null ? null : JsonSerializer.Deserialize<MigrationRecord>(row.Payload);
    }

    public async Task<IReadOnlyList<MigrationRecord>> ListAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Migrations.AsNoTracking()
            .Where(item => item.Environment == _environment)
            .ToListAsync(cancellationToken);
        return rows.Select(row => JsonSerializer.Deserialize<MigrationRecord>(row.Payload)!).ToList();
    }
}
