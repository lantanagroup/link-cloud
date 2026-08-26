namespace LantanaGroup.Automation.Configuration;

/// <summary>
/// Base configuration for a test scenario (smoke test, stress test, etc.).
/// Platform-specific scenarios extend this with domain-specific settings.
/// </summary>
public class TestScenarioConfigBase
{
    /// <summary>
    /// Seconds between polling attempts when waiting for async pipeline completion.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Maximum wall-clock minutes the polling loop will run before timing out.
    /// Zero or negative indicates unlimited polling.
    /// </summary>
    public int MaxPollingDurationMinutes { get; set; } = 3;

    /// <summary>
    /// Optional file:// or http(s):// location of a measure bundle. Runs that already
    /// have <c>MeasureBundleJsons</c> do not use this.
    /// </summary>
    public string MeasureBundleLocation { get; set; } = "";

    /// <summary>
    /// ISO 8601 start date for the reporting period.
    /// </summary>
    public string StartDate { get; set; } = "2023-01-01T00:00:00Z";

    /// <summary>
    /// ISO 8601 end date for the reporting period.
    /// </summary>
    public string EndDate { get; set; } = "2023-12-31T23:59:59Z";

    /// <summary>
    /// Patient identifiers for the scenario.
    /// </summary>
    public List<string> PatientIds { get; set; } = [];

    /// <summary>
    /// Whether to clean up any created infrastructure after the test completes.
    /// </summary>
    public bool CleanupAfterTest { get; set; } = true;

    /// <summary>
    /// Optional file name for download artifacts.
    /// </summary>
    public string DownloadFileName { get; set; } = "submission.zip";

    /// <summary>
    /// Minutes of historical logs to scrape when diagnosing failures.
    /// </summary>
    public int LogScrapeWindowMinutes { get; set; } = 5;

    /// <summary>
    /// The maximum wall-clock time the polling loop will run.
    /// Zero or negative <see cref="MaxPollingDurationMinutes"/> indicates unlimited polling.
    /// </summary>
    public TimeSpan MaxPollingDuration => MaxPollingDurationMinutes <= 0
        ? TimeSpan.MaxValue
        : TimeSpan.FromMinutes(MaxPollingDurationMinutes);

    /// <summary>
    /// The log scrape window as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan LogScrapeWindow => TimeSpan.FromMinutes(LogScrapeWindowMinutes);

    /// <summary>
    /// The polling interval as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan PollingInterval => TimeSpan.FromSeconds(PollingIntervalSeconds);
}
