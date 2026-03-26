namespace LantanaGroup.Link.Automation.Configuration;

/// <summary>
/// Configuration for a specific test scenario (smoke test, mega-patient test, etc.).
/// Consumers set the properties directly rather than relying on environment variables.
/// </summary>
public class TestScenarioConfig
{
    public string MeasureBundleLocation { get; set; } = "";
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
    /// </summary>
    public TimeSpan MaxPollingDuration => TimeSpan.FromSeconds(MaxRetryCount * PollingIntervalSeconds);

    /// <summary>
    /// The Loki scrape window as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan LokiScrapeWindow => TimeSpan.FromMinutes(LokiScrapeWindowMinutes);
}
