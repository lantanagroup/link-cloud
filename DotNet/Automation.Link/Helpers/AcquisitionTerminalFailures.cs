namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// The acquisition log statuses that end a log as a failure, so the automation monitors and validators agree on them.
/// </summary>
public static class AcquisitionTerminalFailures
{
    /// <summary>
    /// The Max Retries Reached status name.
    /// </summary>
    public const string MaxRetriesReached = "MaxRetriesReached";

    /// <summary>
    /// The Access Denied status name, used when the EHR refuses a request with 403 or a repeated 401.
    /// </summary>
    public const string AccessDenied = "AccessDenied";

    /// <summary>
    /// Every status that ends an acquisition log as a failure.
    /// </summary>
    public static readonly IReadOnlyList<string> Statuses = [MaxRetriesReached, AccessDenied];

    /// <summary>
    /// Returns true when <paramref name="status"/> ends an acquisition log as a failure, ignoring case.
    /// </summary>
    public static bool IsTerminalFailure(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return Statuses.Contains(status, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Counts the Max Retries Reached and Access Denied logs in a report's status breakdown.
    /// </summary>
    public static (int MaxRetriesReached, int AccessDenied) Count(
        IEnumerable<PipelineDataReader.StatusCountInfo>? statusCounts)
    {
        if (statusCounts == null)
        {
            return (0, 0);
        }

        var counts = statusCounts.ToList();

        return (CountOf(counts, MaxRetriesReached), CountOf(counts, AccessDenied));
    }

    private static int CountOf(IEnumerable<PipelineDataReader.StatusCountInfo> counts, string status)
    {
        return counts
            .Where(c => string.Equals(c.Status, status, StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.Count);
    }
}
