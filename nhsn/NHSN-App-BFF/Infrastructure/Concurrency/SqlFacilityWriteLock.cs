using System.Data;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Infrastructure;
using LantanaGroup.Link.Nhsn.App.Bff.Persistence;
using LantanaGroup.Link.Nhsn.App.Bff.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Concurrency;

// IFacilityWriteLock over SQL Server's sp_getapplock. Follows the pattern already used twice in
// Data Acquisition (ReferenceResourcesManager.AcquireLockAsync, ReferenceResourceService) rather
// than inventing one — same procedure, same parameters, same colon-namespaced resource key.
//
// @LockOwner = 'Transaction' is what makes this self-cleaning: the lock releases on commit,
// rollback and connection loss. If the pod is killed mid-write, SQL Server rolls the transaction
// back and the lock evaporates — no orphans, no TTL to tune.
//
// Cost: a database transaction stays open across the HTTP calls to Link, holding a pooled
// connection across network I/O, bounded by the lock timeout and the per-call HTTP deadline.
// Acceptable at onboarding volumes; worth revisiting if that assumption changes.
internal sealed class SqlFacilityWriteLock : IFacilityWriteLock
{
    private const string AcquireSql = """
        DECLARE @result int;
        EXEC @result = sp_getapplock
            @Resource = @resource,
            @LockMode = 'Exclusive',
            @LockOwner = 'Transaction',
            @LockTimeout = @lockTimeoutMs,
            @DbPrincipal = 'public';
        SELECT @result;
        """;

    private readonly NhsnAppDbContext _dbContext;
    private readonly FacilityWriteLockSettings _settings;
    private readonly ILogger<SqlFacilityWriteLock> _logger;

    public SqlFacilityWriteLock(NhsnAppDbContext dbContext, IOptions<FacilityWriteLockSettings> settings, ILogger<SqlFacilityWriteLock> logger)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IFacilityWriteLockHandle> AcquireAsync(string facilityId, CancellationToken cancellationToken = default)
    {
        // Namespaced so it cannot collide with Data Acquisition's locks if the two ever share a
        // database, and keyed per facility so two different facilities never block each other.
        var resource = $"NhsnOnboarding:{facilityId}";

        var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        int result;
        try
        {
            result = await ExecuteAcquireAsync(resource, cancellationToken);
        }
        catch
        {
            // The lock was never taken, so the transaction has no purpose. Discard it before
            // letting the failure propagate, or the connection stays checked out of the pool.
            await DiscardAsync(transaction, cancellationToken);
            throw;
        }

        // 0 granted, 1 granted after waiting. Negative values are all failures: -1 timeout,
        // -2 cancelled, -3 deadlock victim, -999 parameter error.
        if (result < 0)
        {
            _logger.LogWarning("sp_getapplock returned {Result} for {Resource} after {TimeoutMs}ms.",
                result, resource, _settings.TimeoutMs);

            await DiscardAsync(transaction, cancellationToken);
            throw new FacilityWriteLockTimeoutException(facilityId, _settings.TimeoutMs);
        }

        return new LockHandle(transaction);
    }

    private async Task<int> ExecuteAcquireAsync(string resource, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();

        command.CommandText = AcquireSql;
        command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("The application lock requires an active transaction.");

        var resourceParam = command.CreateParameter();
        resourceParam.ParameterName = "@resource";
        resourceParam.Value = resource;
        command.Parameters.Add(resourceParam);

        var timeoutParam = command.CreateParameter();
        timeoutParam.ParameterName = "@lockTimeoutMs";
        timeoutParam.Value = _settings.TimeoutMs;
        command.Parameters.Add(timeoutParam);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    // Rolls back and disposes a transaction that never took the lock.
    private static async Task DiscardAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // The transaction may already be gone if the connection dropped — which is also the
            // case where the lock has already released. Dispose is still required either way.
        }

        await transaction.DisposeAsync();
    }

    // Rolls back on dispose unless the caller explicitly committed — a caller that throws before
    // calling CommitAsync must not have any of its writes persisted, including ones already staged
    // by an earlier SaveChangesAsync within the same guarded block.
    private sealed class LockHandle : IFacilityWriteLockHandle
    {
        private readonly IDbContextTransaction _transaction;
        private bool _committed;

        public LockHandle(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            await _transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_committed)
                {
                    await _transaction.RollbackAsync();
                }
            }
            finally
            {
                await _transaction.DisposeAsync();
            }
        }
    }
}
