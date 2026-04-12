namespace LantanaGroup.Automation.Helpers;

public static class RetryHelper
{
    /// <summary>
    /// Retries the given action until it succeeds or the timeout is reached.
    /// Optionally invokes a diagnostics callback on final failure.
    /// </summary>
    public static async Task RetryUntilSuccess(
        Func<Task> action,
        TimeSpan timeout,
        TimeSpan delay,
        IAutomationOutput? output = null,
        Func<Task>? onFinalFailureDiagnostics = null)
    {
        var start = DateTime.UtcNow;
        Exception? lastException = null;
        var attempt = 0;

        while (DateTime.UtcNow - start < timeout)
        {
            attempt++;
            try
            {
                await action();
                if (attempt > 1)
                    output?.WriteLine($"  Succeeded on attempt {attempt}");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                var elapsed = DateTime.UtcNow - start;
                output?.WriteLine($"  Attempt {attempt} failed ({elapsed.TotalSeconds:F0}s elapsed): {ex.Message}");
                output?.WriteLine($"  Retrying in {delay.TotalSeconds:F0}s...");
                await Task.Delay(delay);
            }
        }

        if (onFinalFailureDiagnostics != null)
            await onFinalFailureDiagnostics();

        throw new TimeoutException(
            $"Operation failed after {attempt} attempt(s) over {timeout.TotalSeconds}s.",
            lastException);
    }

    /// <summary>
    /// Retries the given function until it returns a result or the timeout is reached.
    /// </summary>
    public static async Task<T> RetryUntilSuccess<T>(
        Func<Task<T>> action,
        TimeSpan timeout,
        TimeSpan delay,
        IAutomationOutput? output = null,
        Func<Task>? onFinalFailureDiagnostics = null)
    {
        var start = DateTime.UtcNow;
        Exception? lastException = null;
        var attempt = 0;

        while (DateTime.UtcNow - start < timeout)
        {
            attempt++;
            try
            {
                var result = await action();
                if (attempt > 1)
                    output?.WriteLine($"  Succeeded on attempt {attempt}");
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                var elapsed = DateTime.UtcNow - start;
                output?.WriteLine($"  Attempt {attempt} failed ({elapsed.TotalSeconds:F0}s elapsed): {ex.Message}");
                output?.WriteLine($"  Retrying in {delay.TotalSeconds:F0}s...");
                await Task.Delay(delay);
            }
        }

        if (onFinalFailureDiagnostics != null)
            await onFinalFailureDiagnostics();

        throw new TimeoutException(
            $"Operation failed after {attempt} attempt(s) over {timeout.TotalSeconds}s.",
            lastException);
    }

    /// <summary>
    /// Retries with exponential backoff. The delay doubles on each attempt,
    /// capped at <paramref name="maxDelay"/>.
    /// </summary>
    public static async Task RetryWithBackoff(
        Func<Task> action,
        int maxAttempts,
        TimeSpan initialDelay,
        TimeSpan? maxDelay = null,
        IAutomationOutput? output = null,
        Func<Task>? onFinalFailureDiagnostics = null)
    {
        var cap = maxDelay ?? TimeSpan.FromMinutes(2);
        Exception? lastException = null;
        var currentDelay = initialDelay;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                if (attempt > 1)
                    output?.WriteLine($"  Succeeded on attempt {attempt}");
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                output?.WriteLine($"  Attempt {attempt}/{maxAttempts} failed: {ex.Message}");

                if (attempt < maxAttempts)
                {
                    output?.WriteLine($"  Retrying in {currentDelay.TotalSeconds:F0}s...");
                    await Task.Delay(currentDelay);
                    currentDelay = TimeSpan.FromTicks(Math.Min(currentDelay.Ticks * 2, cap.Ticks));
                }
            }
        }

        if (onFinalFailureDiagnostics != null)
            await onFinalFailureDiagnostics();

        throw new InvalidOperationException(
            $"Operation failed after {maxAttempts} attempt(s).",
            lastException);
    }
}
