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

    /// <summary>
    /// Inline FHIR measure-bundle JSON payloads. When non-empty, <c>MeasureLoader</c>
    /// loads these instead of <see cref="AllMeasureBundleLocations"/>.
    /// </summary>
    public List<string> MeasureBundleJsons { get; set; } = [];

    public string StartDate { get; set; } = "2023-01-01T00:00:00Z";
    public string EndDate { get; set; } = "2023-12-31T23:59:59Z";
    public string NhsnOrganizationId { get; set; } = string.Empty;
    public List<string> PatientIds { get; set; } = ["207727"];

    /// <summary>
    /// Remove facility config, soft-delete reports, DA logs, and query dispatch config after the run.
    /// </summary>
    public bool CleanupServiceData { get; set; }

    /// <summary>
    /// Expunge all data from the FHIR server after the run.
    /// </summary>
    public bool CleanupFhirData { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 3;

    /// <summary>
    /// Maximum wall-clock minutes the polling loop will run before timing out.
    /// Zero or negative indicates unlimited polling.
    /// </summary>
    public int MaxPollingDurationMinutes { get; set; } = 3;

    public string DownloadFileName { get; set; } = "submission.zip";
    public int LokiScrapeWindowMinutes { get; set; } = 5;

    /// <summary>
    /// When true, ad-hoc report generation mints X-Metrics-Mode=performance.
    /// </summary>
    public bool IsMetricsRun { get; set; }

    /// <summary>
    /// The maximum wall-clock time the polling loop will run.
    /// Zero or negative <see cref="MaxPollingDurationMinutes"/> indicates unlimited polling.
    /// </summary>
    public TimeSpan MaxPollingDuration => MaxPollingDurationMinutes <= 0
        ? TimeSpan.MaxValue
        : TimeSpan.FromMinutes(MaxPollingDurationMinutes);

    /// <summary>
    /// The Loki scrape window as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan LokiScrapeWindow => TimeSpan.FromMinutes(LokiScrapeWindowMinutes);
}
