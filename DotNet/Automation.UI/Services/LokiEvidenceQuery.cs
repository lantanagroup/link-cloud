using LantanaGroup.Automation.Helpers;

namespace Automation.UI.Services;

/// <summary>
/// Loki ingestion can lag behind Normalization, so a single scrape at validator
/// time sometimes returns zero <c>[NormalizationExecutionSummary]</c> lines even
/// though the service already processed the run. Retry with a delay and a wider
/// lookback before treating missing evidence as a suite failure.
/// </summary>
public static class LokiEvidenceQuery
{
    public const int MaxAttempts = 4;

    public static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20)
    ];

    public static readonly TimeSpan[] WidenedWindows =
    [
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(20)
    ];

    public static TimeSpan LookbackForAttempt(TimeSpan configuredWindow, int attemptIndex)
    {
        if (attemptIndex <= 0 || attemptIndex > WidenedWindows.Length)
            return configuredWindow;

        var widened = WidenedWindows[attemptIndex - 1];
        return configuredWindow > widened ? configuredWindow : widened;
    }

    public static TimeSpan? DelayBeforeAttempt(int attemptIndex)
    {
        if (attemptIndex <= 0 || attemptIndex > RetryDelays.Length)
            return null;

        return RetryDelays[attemptIndex - 1];
    }

    /// <summary>
    /// Retry when the scrape is empty, or when an acquired query-plan type that the
    /// suite targets has no parsed summary lines. Patient is the acquisition anchor
    /// and is not published on ResourcesAcquired cache keys, so missing Patient
    /// evidence alone does not trigger another scrape.
    /// </summary>
    public static bool NeedsRetry(
        IReadOnlyCollection<string> evidenceRequiredResourceTypes,
        IReadOnlyCollection<string>? acquiredResourceTypes,
        IReadOnlyList<string> collectedLogs)
    {
        if (collectedLogs.Count == 0)
            return evidenceRequiredResourceTypes.Count > 0;

        var required = RequiredAcquiredTypes(evidenceRequiredResourceTypes, acquiredResourceTypes);
        if (required.Count == 0)
            return false;

        var found = ResourceTypesInLogs(collectedLogs);
        foreach (var resourceType in required)
        {
            if (!found.Contains(resourceType))
                return true;
        }

        return false;
    }

    public static HashSet<string> ResourceTypesInLogs(IEnumerable<string> logs)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in NormalizationExecutionSummaryParser.ParseAll(logs))
        {
            if (!string.IsNullOrWhiteSpace(step.ResourceType))
                found.Add(step.ResourceType);
        }

        return found;
    }

    public static async Task<List<string>> CollectWithRetryAsync(
        TimeSpan configuredWindow,
        IReadOnlyCollection<string> evidenceRequiredResourceTypes,
        IReadOnlyCollection<string>? acquiredResourceTypes,
        Func<TimeSpan, CancellationToken, Task<List<string>>> queryAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        IAutomationOutput output,
        CancellationToken cancellationToken = default)
    {
        List<string> logs = [];

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var delay = DelayBeforeAttempt(attempt);
            var lookback = LookbackForAttempt(configuredWindow, attempt);

            if (delay is { } wait)
            {
                output.WriteLine(
                    $"[Normalization Suite] Loki evidence incomplete; waiting {wait.TotalSeconds:F0}s then retrying with lookback {lookback.TotalMinutes:F0}m " +
                    $"(attempt {attempt + 1}/{MaxAttempts}).");
                await delayAsync(wait, cancellationToken);
            }

            logs = await queryAsync(lookback, cancellationToken);
            output.WriteLine(
                $"[Normalization Suite] Loki scrape attempt {attempt + 1}/{MaxAttempts}: {logs.Count} summary line(s) " +
                $"(lookback {lookback.TotalMinutes:F0}m).");

            if (!NeedsRetry(evidenceRequiredResourceTypes, acquiredResourceTypes, logs))
                return logs;
        }

        output.WriteLine(
            $"[Normalization Suite] Loki evidence still incomplete after {MaxAttempts} attempt(s); proceeding with {logs.Count} summary line(s).");
        return logs;
    }

    private static HashSet<string> RequiredAcquiredTypes(
        IReadOnlyCollection<string> evidenceRequiredResourceTypes,
        IReadOnlyCollection<string>? acquiredResourceTypes)
    {
        var required = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var acquired = acquiredResourceTypes is { Count: > 0 }
            ? new HashSet<string>(acquiredResourceTypes, StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var resourceType in evidenceRequiredResourceTypes)
        {
            if (string.IsNullOrWhiteSpace(resourceType))
                continue;

            if (acquired != null && !acquired.Contains(resourceType))
                continue;

            required.Add(resourceType);
        }

        return required;
    }
}
