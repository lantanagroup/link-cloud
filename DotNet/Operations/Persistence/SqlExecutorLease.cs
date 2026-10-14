using LantanaGroup.Link.Operations.Application.KafkaOps;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Operations.Persistence;

/// <summary>
/// One executor per environment across every Operations replica. The row is claimed with a conditional update,
/// kept alive while it is held, and released on dispose. If a replica dies, the lease expires and another takes it
/// with a higher fence, which the migration store uses to refuse writes from the old holder.
/// </summary>
public sealed class SqlExecutorLease : IKafkaOpsLease, IDisposable
{
    private readonly IDbContextFactory<OperationsDbContext> _factory;
    private readonly string _name;
    private readonly string _owner;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _time;
    private readonly ILogger<SqlExecutorLease>? _logger;
    // Not re-entrant, like the in-memory lease: tracking and a migration step in the same process never overlap.
    private readonly SemaphoreSlim _local = new(1, 1);

    public SqlExecutorLease(IDbContextFactory<OperationsDbContext> factory, string environment, TimeSpan? ttl = null, string? owner = null, TimeProvider? time = null, ILogger<SqlExecutorLease>? logger = null)
    {
        _factory = factory;
        _name = "kafka-ops:" + environment + ":executor";
        _owner = owner ?? Environment.MachineName + ":" + Environment.ProcessId + ":" + Guid.NewGuid().ToString("N")[..8];
        _ttl = ttl ?? TimeSpan.FromSeconds(60);
        _time = time ?? TimeProvider.System;
        _logger = logger;
    }

    public string Owner => _owner;

    public async Task<KafkaOpsLeaseHold> AcquireAsync(CancellationToken cancellationToken)
    {
        if (!await _local.WaitAsync(TimeSpan.Zero, cancellationToken))
            throw new LeaseHeldException();
        try
        {
            return await ClaimAsync(cancellationToken);
        }
        catch
        {
            _local.Release();
            throw;
        }
    }

    private async Task<KafkaOpsLeaseHold> ClaimAsync(CancellationToken cancellationToken)
    {
        await using (var db = await _factory.CreateDbContextAsync(cancellationToken))
        {
            if (!await db.ExecutorLeases.AnyAsync(item => item.Name == _name, cancellationToken))
            {
                db.ExecutorLeases.Add(new ExecutorLeaseEntity { Name = _name, Owner = null, ExpiresUnixMs = 0, Fence = 0 });
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Another replica created the row first.
                }
            }
        }

        long fence;
        await using (var db = await _factory.CreateDbContextAsync(cancellationToken))
        {
            var now = Now();
            var expires = now + (long)_ttl.TotalMilliseconds;
            var claimed = await db.ExecutorLeases
                .Where(item => item.Name == _name && (item.Owner == null || item.ExpiresUnixMs < now))
                .ExecuteUpdateAsync(set => set
                    .SetProperty(item => item.Owner, _owner)
                    .SetProperty(item => item.ExpiresUnixMs, expires)
                    .SetProperty(item => item.Fence, item => item.Fence + 1), cancellationToken);
            if (claimed != 1)
                throw new LeaseHeldException();
            fence = await db.ExecutorLeases.AsNoTracking()
                .Where(item => item.Name == _name)
                .Select(item => item.Fence)
                .FirstAsync(cancellationToken);
        }

        var renewal = new CancellationTokenSource();
        var period = TimeSpan.FromMilliseconds(Math.Max(1000, _ttl.TotalMilliseconds / 3));
        _ = Task.Run(() => RenewAsync(fence, period, renewal.Token), CancellationToken.None);
        return new KafkaOpsLeaseHold(fence, () =>
        {
            renewal.Cancel();
            renewal.Dispose();
            try
            {
                ReleaseAsync(fence).GetAwaiter().GetResult();
            }
            finally
            {
                _local.Release();
            }
        });
    }

    private async Task RenewAsync(long fence, TimeSpan period, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await using var db = await _factory.CreateDbContextAsync(cancellationToken);
                var expires = Now() + (long)_ttl.TotalMilliseconds;
                var kept = await db.ExecutorLeases
                    .Where(item => item.Name == _name && item.Owner == _owner && item.Fence == fence)
                    .ExecuteUpdateAsync(set => set.SetProperty(item => item.ExpiresUnixMs, expires), cancellationToken);
                if (kept != 1)
                {
                    _logger?.LogWarning("Kafka executor lease {Fence} was lost before it was released", fence);
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Kafka executor lease renewal failed");
        }
    }

    private async Task ReleaseAsync(long fence)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync();
            await db.ExecutorLeases
                .Where(item => item.Name == _name && item.Owner == _owner && item.Fence == fence)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(item => item.Owner, (string?)null)
                    .SetProperty(item => item.ExpiresUnixMs, 0L));
        }
        catch (Exception ex)
        {
            // The lease still expires on its own.
            _logger?.LogWarning(ex, "Kafka executor lease release failed");
        }
    }

    private long Now() => _time.GetUtcNow().ToUnixTimeMilliseconds();

    public void Dispose() => _local.Dispose();
}
