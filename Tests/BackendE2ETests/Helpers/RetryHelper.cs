using Xunit.Abstractions;

namespace LantanaGroup.Link.Tests.E2ETests.Helpers;

public static class RetryHelper
{
    public static async Task RetryUntilSuccess(
        Func<Task> action,
        TimeSpan timeout,
        TimeSpan delay,
        ITestOutputHelper? output = null,
        LokiScraper? lokiScraper = null)
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

        if (lokiScraper != null)
            await lokiScraper.ScrapeErrorsAsync();

        throw new TimeoutException(
            $"Operation failed after {attempt} attempt(s) over {timeout.TotalSeconds}s.",
            lastException);
    }
}
