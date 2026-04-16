namespace LantanaGroup.Automation.Helpers;

/// <summary>
/// Generic status-polling loop with diagnostics integration.
/// Extracts the common pattern of "poll an API until a condition is met,
/// with timeout, progress output, and optional early-exit on critical failure."
/// </summary>
public static class StatusPollingHelper
{
    /// <summary>
    /// Polls <paramref name="getStatus"/> at the configured interval until
    /// <paramref name="isComplete"/> returns true, or the timeout is reached.
    /// Returns (succeeded, finalStatus).
    /// </summary>
    /// <param name="getStatus">Async function that returns the current status string.</param>
    /// <param name="isComplete">Predicate that returns true when the status represents completion.</param>
    /// <param name="timeout">Maximum wall-clock time to poll.</param>
    /// <param name="pollingInterval">Delay between polls.</param>
    /// <param name="output">Optional output for progress logging.</param>
    /// <param name="label">Label for log messages (e.g., "Report submission").</param>
    /// <param name="shouldAbort">Optional predicate checked each cycle to trigger early exit (e.g., critical failure detected).</param>
    /// <param name="abortMessage">Message logged when <paramref name="shouldAbort"/> triggers.</param>
    public static async Task<(bool Succeeded, string? FinalStatus)> PollUntilAsync(
        Func<Task<string?>> getStatus,
        Func<string?, bool> isComplete,
        TimeSpan timeout,
        TimeSpan pollingInterval,
        IAutomationOutput? output = null,
        string label = "Status",
        Func<bool>? shouldAbort = null,
        string? abortMessage = null)
    {
        var start = DateTime.UtcNow;
        string? lastStatus = null;
        var pollCount = 0;

        output?.WriteLine(timeout == TimeSpan.MaxValue
            ? $"Polling for {label} (no timeout)..."
            : $"Polling for {label} (max {timeout.TotalSeconds:F0}s)...");

        while (DateTime.UtcNow - start < timeout)
        {
            pollCount++;

            if (shouldAbort?.Invoke() == true)
            {
                output?.WriteLine(abortMessage ?? $"[EARLY EXIT] Abort condition triggered — stopping {label} poll loop.");
                return (false, lastStatus);
            }

            try
            {
                var currentStatus = await getStatus();

                if (isComplete(currentStatus))
                {
                    var elapsed = DateTime.UtcNow - start;
                    output?.WriteLine($"{label} completed: '{currentStatus}' (after {elapsed.TotalSeconds:F0}s, {pollCount} polls).");
                    return (true, currentStatus);
                }

                if (!string.Equals(currentStatus, lastStatus, StringComparison.Ordinal))
                {
                    output?.WriteLine($"[Poll] {label}: {currentStatus ?? "(null)"}");
                    lastStatus = currentStatus;
                }
            }
            catch (Exception ex)
            {
                if (!string.Equals(ex.Message, lastStatus, StringComparison.Ordinal))
                {
                    output?.WriteLine($"[Poll] {label} error: {ex.Message}");
                    lastStatus = ex.Message;
                }
            }

            await Task.Delay(pollingInterval);
        }

        output?.WriteLine($"{label} did not complete within {timeout.TotalSeconds:F0}s ({pollCount} polls).");
        return (false, lastStatus);
    }

    /// <summary>
    /// Polls until a typed result satisfies a predicate.
    /// Returns (succeeded, finalResult).
    /// </summary>
    public static async Task<(bool Succeeded, T? FinalResult)> PollUntilAsync<T>(
        Func<Task<T?>> getResult,
        Func<T?, bool> isComplete,
        TimeSpan timeout,
        TimeSpan pollingInterval,
        IAutomationOutput? output = null,
        string label = "Status",
        Func<T?, string?>? describeResult = null,
        Func<bool>? shouldAbort = null,
        string? abortMessage = null)
    {
        var start = DateTime.UtcNow;
        string? lastDescription = null;
        T? lastResult = default;
        var pollCount = 0;

        output?.WriteLine(timeout == TimeSpan.MaxValue
            ? $"Polling for {label} (no timeout)..."
            : $"Polling for {label} (max {timeout.TotalSeconds:F0}s)...");

        while (DateTime.UtcNow - start < timeout)
        {
            pollCount++;

            if (shouldAbort?.Invoke() == true)
            {
                output?.WriteLine(abortMessage ?? $"[EARLY EXIT] Abort condition triggered — stopping {label} poll loop.");
                return (false, lastResult);
            }

            try
            {
                var result = await getResult();
                lastResult = result;

                if (isComplete(result))
                {
                    var elapsed = DateTime.UtcNow - start;
                    var desc = describeResult?.Invoke(result) ?? result?.ToString();
                    output?.WriteLine($"{label} completed: {desc} (after {elapsed.TotalSeconds:F0}s, {pollCount} polls).");
                    return (true, result);
                }

                var currentDescription = describeResult?.Invoke(result) ?? result?.ToString();
                if (!string.Equals(currentDescription, lastDescription, StringComparison.Ordinal))
                {
                    output?.WriteLine($"[Poll] {label}: {currentDescription ?? "(null)"}");
                    lastDescription = currentDescription;
                }
            }
            catch (Exception ex)
            {
                if (!string.Equals(ex.Message, lastDescription, StringComparison.Ordinal))
                {
                    output?.WriteLine($"[Poll] {label} error: {ex.Message}");
                    lastDescription = ex.Message;
                }
            }

            await Task.Delay(pollingInterval);
        }

        output?.WriteLine($"{label} did not complete within {timeout.TotalSeconds:F0}s ({pollCount} polls).");
        return (false, lastResult);
    }
}
