using System.Data;
using System.Data.Common;
using System.Reflection;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LantanaGroup.Link.Normalization.Domain;

internal enum ReadCommittedSnapshotResult
{
    AlreadyEnabled,
    Enabled,
    SkippedBlocked,
    SkippedPermission,
    SkippedError,
    SkippedNotRelational
}

/// <summary>
/// How startup retries enabling snapshot reads. The default is a few attempts spread over about 10 to 15 seconds.
/// </summary>
internal sealed class ReadCommittedSnapshotRetry
{
    public int MaxAttempts { get; init; } = 4;

    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(3500);

    public TimeSpan Jitter { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan AlterTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public static ReadCommittedSnapshotRetry Default { get; } = new();
}

/// <summary>
/// Turns on snapshot reads for the Normalization database only.
/// Deployments restart every Normalization replica together, so no old replica is holding a session.
/// A replica that is waiting still counts as a session, and SQL Server will not change this option while
/// another user session is connected. There is no cross-replica application lock for that reason.
/// Each attempt reads the flag and, when it is off, counts other user sessions. If any exist, this process
/// disconnects and waits a short jittered delay so one of the starting replicas can find a quiet window.
/// The alter uses a short lock timeout and never WITH ROLLBACK IMMEDIATE. When the tries are used up, or the
/// login cannot alter the database, a warning is logged and startup continues.
/// Azure SQL already has the option on, so the check returns before any alter.
/// </summary>
internal static class ReadCommittedSnapshotEnabler
{
    public static Task<ReadCommittedSnapshotResult> EnsureAsync(
        NormalizationDbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        return EnsureAsync(context, logger, ReadCommittedSnapshotRetry.Default, cancellationToken);
    }

    public static async Task<ReadCommittedSnapshotResult> EnsureAsync(
        NormalizationDbContext context,
        ILogger logger,
        ReadCommittedSnapshotRetry retry,
        CancellationToken cancellationToken)
    {
        if (!context.Database.IsRelational())
        {
            return ReadCommittedSnapshotResult.SkippedNotRelational;
        }

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        var timeoutChanged = false;
        var alterTimeoutMs = (int)Math.Clamp(retry.AlterTimeout.TotalMilliseconds, 1, 60_000);
        var maxAttempts = Math.Max(1, retry.MaxAttempts);

        try
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(cancellationToken);
                    openedHere = true;
                }

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

                    var others = await CountOtherUserSessionsAsync(connection, logger, cancellationToken);
                    if (others is > 0)
                    {
                        if (attempt == maxAttempts)
                        {
                            logger.LogWarning(
                                "Snapshot reads were not enabled on Normalization database {Database} because other sessions are connected. Startup continues.",
                                connection.Database);
                            return ReadCommittedSnapshotResult.SkippedBlocked;
                        }

                        logger.LogInformation(
                            "Other sessions are connected to Normalization database {Database}. Skipping this attempt to enable snapshot reads.",
                            connection.Database);
                        await DisconnectAndWaitAsync();
                        continue;
                    }

                    await ExecuteNonQueryAsync(connection, $"SET LOCK_TIMEOUT {alterTimeoutMs}", alterTimeoutMs, cancellationToken);
                    timeoutChanged = true;
                    await ExecuteNonQueryAsync(
                        connection,
                        "ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON",
                        alterTimeoutMs,
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
                    if (attempt == maxAttempts)
                    {
                        logger.LogWarning(
                            ex,
                            "Snapshot reads were not enabled on Normalization database {Database} because another session is connected. Startup continues.",
                            connection.Database);
                        return ReadCommittedSnapshotResult.SkippedBlocked;
                    }

                    logger.LogInformation(ex, "Snapshot reads could not be enabled yet on Normalization database {Database}. Retrying.", connection.Database);
                    await DisconnectAndWaitAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Snapshot reads were not enabled on Normalization database {Database}. Continuing.", connection.Database);
                    return ReadCommittedSnapshotResult.SkippedError;
                }
            }

            logger.LogWarning("Snapshot reads were not enabled on Normalization database {Database}. Continuing.", connection.Database);
            return ReadCommittedSnapshotResult.SkippedBlocked;
        }
        finally
        {
            if (timeoutChanged)
            {
                await TryExecuteNonQueryAsync(connection, "SET LOCK_TIMEOUT -1", cancellationToken);
            }

            if (openedHere && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }

        async Task DisconnectAndWaitAsync()
        {
            if (timeoutChanged && connection.State == ConnectionState.Open)
            {
                await TryExecuteNonQueryAsync(connection, "SET LOCK_TIMEOUT -1", cancellationToken);
                timeoutChanged = false;
            }

            // Close our own pooled sessions before waiting. Another replica is a different process and is left alone.
            // Returning a connection to the pool would keep a session open and block that replica's alter.
            ReleaseThisProcessPool(connection);
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }

            var wait = NextDelay(retry);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }
        }
    }

    private static TimeSpan NextDelay(ReadCommittedSnapshotRetry retry)
    {
        var jitterMs = 0;
        if (retry.Jitter > TimeSpan.Zero)
        {
            var upper = (int)Math.Clamp(retry.Jitter.TotalMilliseconds, 0, 60_000);
            jitterMs = Random.Shared.Next(0, upper + 1);
        }

        var delay = retry.Delay + TimeSpan.FromMilliseconds(jitterMs);
        return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
    }

    private static void ReleaseThisProcessPool(DbConnection connection)
    {
        try
        {
            var type = connection.GetType();
            var clearPool = type.GetMethod(
                "ClearPool",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { type },
                modifiers: null);
            clearPool?.Invoke(null, new object[] { connection });
        }
        catch (Exception)
        {
            // Closing this connection is still required. Other processes are not affected by this pool.
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

    private static async Task<int?> CountOtherUserSessionsAsync(
        DbConnection connection,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sys.dm_exec_sessions
                WHERE database_id = DB_ID()
                  AND session_id <> @@SPID
                  AND is_user_process = 1
                """;
            command.CommandTimeout = 30;
            var raw = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(raw);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Could not count other sessions on Normalization database {Database}. This attempt will try the alter.", connection.Database);
            return null;
        }
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = Math.Max(1, (int)Math.Ceiling(timeoutMs / 1000.0));
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
