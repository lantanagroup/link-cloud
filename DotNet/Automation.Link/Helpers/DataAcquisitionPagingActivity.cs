namespace LantanaGroup.Link.Automation.Link.Helpers;

/// <summary>
/// Parses Data Acquisition INFO lines that a log is retrieving paged FHIR results.
/// Keep the token in lockstep with <c>FhirApiService.ExecutePagingSearch</c>.
/// </summary>
public static class DataAcquisitionPagingActivity
{
    public const string LogToken = "retrieving paged results";

    public static string? Summarize(IEnumerable<string> logLines, TimeSpan lookback)
    {
        var lines = logLines
            .Where(line => !string.IsNullOrWhiteSpace(line)
                           && line.Contains(LogToken, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (lines.Count == 0)
            return null;

        var logIds = new HashSet<string>(StringComparer.Ordinal);
        var resourceTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var maxPage = 0;
        var maxCumulative = 0;
        var sawStart = false;

        foreach (var line in lines)
        {
            ParseLogId(line, logIds);
            ParseStartingResourceType(line, resourceTypes, ref sawStart);
            ParsePage(line, resourceTypes, ref maxPage);
            ParseCumulative(line, ref maxCumulative);
        }

        var descriptors = new List<string>();
        if (resourceTypes.Count > 0)
            descriptors.Add(string.Join(", ", resourceTypes.Take(3)));
        if (logIds.Count == 1)
            descriptors.Add($"log {logIds.First()}");
        else if (logIds.Count > 1)
        {
            var sample = string.Join(", ", logIds.Take(3));
            descriptors.Add(logIds.Count > 3
                ? $"logs {sample} (+{logIds.Count - 3} more)"
                : $"logs {sample}");
        }

        if (maxPage > 0)
            descriptors.Add($"page {maxPage}");
        else if (sawStart)
            descriptors.Add("starting");

        var suffix = new List<string>();
        if (maxCumulative > 0)
            suffix.Add($"{maxCumulative} total so far");
        suffix.Add($"{lines.Count} log lines/{lookback.TotalSeconds:F0}s");

        var head = descriptors.Count > 0 ? string.Join(" ", descriptors) : "results";
        return $"paging {head} ({string.Join(", ", suffix)})";
    }

    private static void ParseLogId(string line, ISet<string> logIds)
    {
        var logMarker = "Log ";
        var logIdx = line.IndexOf(logMarker, StringComparison.OrdinalIgnoreCase);
        var tokenIdx = line.IndexOf(LogToken, StringComparison.OrdinalIgnoreCase);
        if (logIdx < 0 || tokenIdx <= logIdx)
            return;

        var idPart = line[(logIdx + logMarker.Length)..tokenIdx].Trim();
        if (long.TryParse(idPart, out _))
            logIds.Add(idPart);
    }

    private static void ParseStartingResourceType(string line, ISet<string> resourceTypes, ref bool sawStart)
    {
        const string startingMarker = "starting ";
        var startIdx = line.IndexOf(startingMarker, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0)
            return;

        sawStart = true;
        var typeStart = startIdx + startingMarker.Length;
        var searchIdx = line.IndexOf(" search", typeStart, StringComparison.OrdinalIgnoreCase);
        if (searchIdx <= typeStart)
            return;

        var resourceType = line[typeStart..searchIdx].Trim();
        if (!string.IsNullOrWhiteSpace(resourceType) && resourceType.Length <= 40)
            resourceTypes.Add(resourceType);
    }

    private static void ParsePage(string line, ISet<string> resourceTypes, ref int maxPage)
    {
        const string pageMarker = " page ";
        var pageIdx = line.IndexOf(pageMarker, StringComparison.OrdinalIgnoreCase);
        if (pageIdx < 0)
            return;

        var afterPage = line[(pageIdx + pageMarker.Length)..];
        var pageEnd = afterPage.IndexOfAny([' ', '(', ',']);
        var pageToken = pageEnd > 0 ? afterPage[..pageEnd] : afterPage;
        if (int.TryParse(pageToken, out var page) && page > maxPage)
            maxPage = page;

        var colonIdx = line.LastIndexOf(':', pageIdx);
        if (colonIdx < 0 || pageIdx <= colonIdx)
            return;

        var resourceType = line[(colonIdx + 1)..pageIdx].Trim();
        if (!string.IsNullOrWhiteSpace(resourceType) && resourceType.Length <= 40)
            resourceTypes.Add(resourceType);
    }

    private static void ParseCumulative(string line, ref int maxCumulative)
    {
        const string totalMarker = " total so far";
        var totalIdx = line.IndexOf(totalMarker, StringComparison.OrdinalIgnoreCase);
        if (totalIdx <= 0)
            return;

        var before = line[..totalIdx];
        var lastSpace = before.LastIndexOf(' ');
        if (lastSpace < 0)
            return;
        if (int.TryParse(before[(lastSpace + 1)..], out var cumulative) && cumulative > maxCumulative)
            maxCumulative = cumulative;
    }
}
