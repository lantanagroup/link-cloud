namespace LantanaGroup.Link.Automation.Link.Configuration;

/// <summary>
/// Configuration for a specific test scenario (smoke test, mega-patient test, etc.).
/// Consumers set the properties directly rather than relying on environment variables.
/// </summary>
public class TestScenarioConfig
{
    public string MeasureBundleLocation { get; set; } = "";

    /// <summary>
    /// Additional measure bundle locations for multi-measure scenarios.
    /// When populated, <see cref="MeasureBundleLocation"/> is treated as the first entry
    /// and these are loaded in addition to it.
    /// </summary>
    public List<string> AdditionalMeasureBundleLocations { get; set; } = [];

    /// <summary>
    /// Convenience accessor that returns all measure bundle locations.
    /// Returns <see cref="MeasureBundleLocation"/> followed by
    /// <see cref="AdditionalMeasureBundleLocations"/>.
    /// </summary>
    public List<string> AllMeasureBundleLocations =>
        string.IsNullOrWhiteSpace(MeasureBundleLocation)
            ? [.. AdditionalMeasureBundleLocations]
            : [MeasureBundleLocation, .. AdditionalMeasureBundleLocations];

    public string StartDate { get; set; } = "2023-01-01T00:00:00Z";
    public string EndDate { get; set; } = "2023-12-31T23:59:59Z";
    public List<string> PatientIds { get; set; } = ["207727"];
    public bool RemoveFacilityConfig { get; set; } = true;
    public bool RemoveReport { get; set; }
    public int PollingIntervalSeconds { get; set; } = 3;
    public int MaxRetryCount { get; set; } = 60;
    public string DownloadFileName { get; set; } = "submission.zip";
    public int LokiScrapeWindowMinutes { get; set; } = 5;

    /// <summary>
    /// The maximum wall-clock time the polling loop will run,
    /// computed from <see cref="MaxRetryCount"/> × <see cref="PollingIntervalSeconds"/>.
    /// Non-positive <see cref="MaxRetryCount"/> indicates unlimited polling.
    /// </summary>
    public TimeSpan MaxPollingDuration => MaxRetryCount <= 0
        ? TimeSpan.MaxValue
        : TimeSpan.FromSeconds(MaxRetryCount * PollingIntervalSeconds);

    /// <summary>
    /// The Loki scrape window as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan LokiScrapeWindow => TimeSpan.FromMinutes(LokiScrapeWindowMinutes);
}
