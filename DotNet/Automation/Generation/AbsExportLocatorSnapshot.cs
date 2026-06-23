namespace LantanaGroup.Automation.Generation;

/// <summary>
/// Lightweight metadata used by diagnostics export to locate/re-download internal ABS
/// artifacts without persisting the full ABS payload in snapshot storage.
/// </summary>
public sealed class AbsExportLocatorSnapshot
{
    public string FacilityId { get; init; } = string.Empty;
    public string ReportId { get; init; } = string.Empty;
    public bool External { get; init; }
    public DateTimeOffset CapturedAt { get; init; }
    public int FileCount { get; init; }
    public long ApproximateTextCharCount { get; init; }
    public IReadOnlyList<string> FileNames { get; init; } = [];

    public static AbsExportLocatorSnapshot Build(string facilityId, string reportId, IDictionary<string, object> files, bool external = false)
    {
        var names = files.Keys
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        long charCount = 0;
        foreach (var value in files.Values)
        {
            charCount += value switch
            {
                string s => s.Length,
                _ => value?.ToString()?.Length ?? 0
            };
        }

        return new AbsExportLocatorSnapshot
        {
            FacilityId = facilityId,
            ReportId = reportId,
            External = external,
            CapturedAt = DateTimeOffset.UtcNow,
            FileCount = names.Count,
            ApproximateTextCharCount = charCount,
            FileNames = names
        };
    }
}
