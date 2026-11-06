using System.Data;
using System.Data.Common;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Normalization.Domain;

internal enum ReadCommittedSnapshotResult
{
    AlreadyEnabled,
    Enabled,
    SkippedInProgress,
    SkippedBlocked,
    SkippedPermission,
    SkippedError,
    SkippedNotRelational
}

/// <summary>
/// Turns on snapshot reads for the Normalization database only.
/// A migration cannot do this safely: SQL Server needs exclusive access to flip the option,
/// a failed migration is either recorded and never retried or it fails startup, and
/// WITH ROLLBACK IMMEDIATE would kill the other replicas during a rolling deploy.
/// Azure SQL already has the option on, so the check returns before any alter.
/// </summary>
internal static class ReadCommittedSnapshotEnabler
{
    public static readonly TimeSpan DefaultLockTimeout = TimeSpan.FromSeconds(5);
    private const string LockResource = "link-normalization-rcsi";

    public static async Task<ReadCommittedSnapshotResult> EnsureAsync(
        NormalizationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        return await EnsureAsync(context, logger, DefaultLockTimeout, cancellationToken);
    }

    public static async Task<ReadCommittedSnapshotResult> EnsureAsync(
        NormalizationDbContext context,
        ILogger logger,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return ReadCommittedSnapshotResult.SkippedNotRelational;
        }

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var lockHeld = false;
        var timeoutChanged = false;
        var lockTimeoutMs = (int)Math.Clamp(lockTimeout.TotalMilliseconds, 1, 60_000);
        try
        {
            var enabled = await ReadEnabledAsync(connection, cancellationToken);
            if (enabled == true)
            {
                logger.LogDebug("Normalization database {Database} already uses snapshot reads.", connection.Database);
                return ReadCommittedSnapshotResult.AlreadyEnabled;
            }

            if (enabled == null)
            {
                logger.LogWarning("Could not read snapshot-read state for Normalization database {Database}. Continuing without changing it.", connection.Database);
                return ReadCommittedSnapshotResult.SkippedError;
            }

            var lockResult = await AppLockAsync(connection, lockTimeoutMs, cancellationToken);
            if (lockResult < 0)
            {
                logger.LogWarning(
                    "Another Normalization replica is enabling snapshot reads on {Database} (sp_getapplock result {LockResult}). Continuing.",
                    connection.Database,
                    lockResult);
                return ReadCommittedSnapshotResult.SkippedInProgress;
            }

            lockHeld = true;
            enabled = await ReadEnabledAsync(connection, cancellationToken);
            if (enabled == true)
            {
                logger.LogDebug("Normalization database {Database} already uses snapshot reads.", connection.Database);
                return ReadCommittedSnapshotResult.AlreadyEnabled;
            }

            await ExecuteNonQueryAsync(connection, $"SET LOCK_TIMEOUT {lockTimeoutMs}", lockTimeout, cancellationToken);
            timeoutChanged = true;
            await ExecuteNonQueryAsync(
                connection,
                "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON",
                lockTimeout,
                cancellationToken);

            logger.LogInformation("Enabled snapshot reads on Normalization database {Database}.", connection.Database);
            return ReadCommittedSnapshotResult.Enabled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (IsPermission(ex))
        {
            logger.LogWarning(ex, "This login cannot alter Normalization database {Database}, so snapshot reads stay as they are. Continuing.", connection.Database);
            return ReadCommittedSnapshotResult.SkippedPermission;
        }
        catch (Exception ex) when (IsBlocked(ex))
        {
            logger.LogWarning(
                ex,
                "Snapshot reads were not enabled on Normalization database {Database} because another session is connected. This start continues. The next start tries again.",
                connection.Database);
            return ReadCommittedSnapshotResult.SkippedBlocked;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Snapshot reads were not enabled on Normalization database {Database}. Continuing.", connection.Database);
            return ReadCommittedSnapshotResult.SkippedError;
        }
        finally
        {
            if (lockHeld)
            {
                await TryReleaseLockAsync(connection, cancellationToken);
            }

            if (timeoutChanged)
            {
                await TryExecuteNonQueryAsync(connection, "SET LOCK_TIMEOUT -1", cancellationToken);
            }
            if (openedHere && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool?> ReadEnabledAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()";
        command.CommandTimeout = 30;
        var raw = await command.ExecuteScalarAsync(cancellationToken);
        if (raw is null or DBNull)
        {
            return null;
        }

        return raw switch
        {
            bool value => value,
            byte value => value != 0,
            short value => value != 0,
            int value => value != 0,
            long value => value != 0,
            _ => Convert.ToInt32(raw) != 0
        };
    }

    private static async Task<int> AppLockAsync(DbConnection connection, int lockTimeoutMs, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Session',
                @LockTimeout = @lockTimeoutMs,
                @DbPrincipal = 'public';
            SELECT @result;
            """;
        command.CommandTimeout = 30;
        AddParameter(command, "@resource", LockResource);
        AddParameter(command, "@lockTimeoutMs", lockTimeoutMs);
        var raw = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(raw);
    }

    private static async Task TryReleaseLockAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                EXEC sp_releaseapplock
                    @Resource = @resource,
                    @LockOwner = 'Session',
                    @DbPrincipal = 'public';
                """;
            command.CommandTimeout = 30;
            AddParameter(command, "@resource", LockResource);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The session lock ends when this connection closes.
        }
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        TimeSpan lockTimeout,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = (int)Math.Ceiling(lockTimeout.TotalSeconds) + 2;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task TryExecuteNonQueryAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        try
        {
            if (connection.State != ConnectionState.Open)
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 30;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Restoring the session default must not hide the enable result.
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static bool IsPermission(Exception exception)
    {
        var number = SqlNumber(exception);
        if (number is 229 or 262 or 297 or 4604 or 5011 or 15247)
        {
            return true;
        }

        return exception.Message.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || (exception.InnerException?.Message.Contains("permission", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool IsBlocked(Exception exception)
    {
        var number = SqlNumber(exception);
        // 1222 is a lock timeout. 5069 is ALTER DATABASE failed while waiting for exclusive access.
        // -2 is the client command timeout, used when the server lock timeout does not cover this alter.
        if (number is 1222 or 5069 or -2 or 1205)
        {
            return true;
        }

        var message = exception.Message;
        return message.Contains("lock request time out", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout expired", StringComparison.OrdinalIgnoreCase)
            || message.Contains("exclusive", StringComparison.OrdinalIgnoreCase);
    }

    private static int? SqlNumber(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.GetType().Name == "SqlException"
                && current.GetType().GetProperty("Number")?.GetValue(current) is int number)
            {
                return number;
            }
        }

        return null;
    }
}
