using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

/// <summary>
/// Retries Cosmos DB for MongoDB RU throttling (HTTP 429, code 16500) with backoff.
/// </summary>
internal static class CosmosThrottle
{
    public const int MaxAttempts = 8;

    /// <summary>Tests set this to <see cref="TimeSpan.Zero"/> so backoff does not wait.</summary>
    internal static TimeSpan? DelayOverride { get; set; }

    public static bool IsThrottle(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is MongoCommandException command && command.Code is 16500 or 429)
                return true;

            if (current is MongoWriteException write && write.WriteError?.Code == 16500)
                return true;

            // BulkWriteAsync reports 16500 on WriteErrors. The exception text is
            // "Code: 16500", which does not match the message phrases below.
            if (current is MongoBulkWriteException bulk)
            {
                foreach (var error in bulk.WriteErrors)
                {
                    if (error.Code is 16500 or 429)
                        return true;
                }

                if (bulk.WriteConcernError?.Code is 16500 or 429)
                    return true;
            }

            var message = current.Message;
            if (message.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Request rate is large", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Error=16500", StringComparison.Ordinal)
                || message.Contains("code 16500", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken ct, ILogger? logger = null)
        => ExecuteAsync(async token =>
        {
            await action(token);
            return true;
        }, ct, logger);

    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct, ILogger? logger = null)
    {
        var delayMs = 200;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && IsThrottle(ex) && attempt < MaxAttempts)
            {
                logger?.LogWarning(
                    "Cosmos request throttled (attempt {Attempt} of {MaxAttempts}). Waiting {DelayMs}ms.",
                    attempt,
                    MaxAttempts,
                    delayMs);
                var delay = DelayOverride ?? TimeSpan.FromMilliseconds(delayMs);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, ct);
                delayMs = Math.Min(delayMs * 2, 10_000);
            }
        }
    }
}
