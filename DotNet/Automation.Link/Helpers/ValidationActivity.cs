namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Parses Validation INFO lines that a long FHIR/categorize/persist step is still
/// running. Keep the token in lockstep with <c>ValidationProgressHeartbeat.LOG_TOKEN</c>.
/// </summary>
public static class ValidationActivity
{
    public const string LogToken = "validation still in progress";

    public static string? Summarize(IEnumerable<string> logLines, TimeSpan lookback)
    {
        var lines = logLines
            .Where(line => !string.IsNullOrWhiteSpace(line)
                           && line.Contains(LogToken, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lines.Count == 0)
            return null;

        var maxElapsed = 0;
        string? detail = null;
        foreach (var line in lines)
        {
            ParseElapsed(line, ref maxElapsed);
            ParseDetail(line, ref detail);
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(detail))
            parts.Add(detail);
        if (maxElapsed > 0)
            parts.Add($"elapsed {maxElapsed}s");
        parts.Add($"{lines.Count} log lines/{lookback.TotalSeconds:F0}s");
        return string.Join(", ", parts);
    }

    private static void ParseDetail(string line, ref string? detail)
    {
        var tokenIdx = line.IndexOf(LogToken, StringComparison.OrdinalIgnoreCase);
        if (tokenIdx < 0)
            return;

        var after = line[(tokenIdx + LogToken.Length)..].Trim();
        if (after.StartsWith(':'))
            after = after[1..].Trim();

        var elapsedIdx = after.LastIndexOf("(elapsed ", StringComparison.OrdinalIgnoreCase);
        if (elapsedIdx > 0)
            after = after[..elapsedIdx].Trim();

        if (string.IsNullOrWhiteSpace(after))
            return;
        if (after.Length > 80)
            after = after[..80];
        detail = after;
    }

    private static void ParseElapsed(string line, ref int maxElapsed)
    {
        const string marker = "(elapsed ";
        var idx = line.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return;

        var after = line[(idx + marker.Length)..];
        var end = after.IndexOf('s');
        var token = end > 0 ? after[..end] : after;
        if (int.TryParse(token, out var elapsed) && elapsed > maxElapsed)
            maxElapsed = elapsed;
    }
}
