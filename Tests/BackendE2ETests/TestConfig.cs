using System.Reflection;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class TestConfig
{
    public static string ExternalFhirServerBase => Environment.GetEnvironmentVariable("EXTERNAL_FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";
    public static string InternalFhirServerBase => Environment.GetEnvironmentVariable("INTERNAL_FHIR_SERVER_BASE_URL") ?? "http://fhir-server:8080/fhir";
    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";
    public static string LokiBaseUrl => Environment.GetEnvironmentVariable("LOKI_BASE_URL") ?? "http://localhost:3100";
    public static string? SmokeTestDownloadPath =>
    Environment.GetEnvironmentVariable("SMOKE_TEST_DOWNLOAD_PATH");
    public static bool CleanupSmokeTestData => bool.Parse(Environment.GetEnvironmentVariable("CLEANUP_SMOKE_TEST_DATA") ?? "true");
    public static OAuthConfig AdminBffOAuth => new("ADMINBFF");
    public static OAuthConfig FhirServerOAuth => new("FHIRSERVER");
    public static BasicAuthConfig FhirServerBasicAuth => new("FHIRSERVER");
    public static SmokeTestConfig AdhocReportingSmokeTestConfig => new("ADHOC_REPORTING_SMOKE_TEST");
    public static SmokeTestConfig MegaPatientTestConfig => new("MEGA_PATIENT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 5,
        defaultMaxRetryCount: 300,
        defaultLokiScrapeWindowMinutes: 20);

    public static class FhirQueryConfig
    {
        public const int MaxConcurrentRequests = 8;
        public static readonly TimeSpan MinAcquisitionPullTime = TimeSpan.FromHours(1);
        public static readonly TimeSpan MaxAcquisitionPullTime = TimeSpan.FromHours(24);
        public static readonly string TimeZone = "America/New_York";
    }

    public static string GetEmbeddedResourceContent(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public class SmokeTestConfig
    {
        private readonly string _prefix;

        public SmokeTestConfig(
            string prefix,
            List<string>? defaultPatientIds = null,
            int defaultPollingIntervalSeconds = 3,
            int defaultMaxRetryCount = 60,
            int defaultLokiScrapeWindowMinutes = 5)
        {
            _prefix = prefix;
            MeasureBundleLocation = Environment.GetEnvironmentVariable($"{prefix}_MEASURE_BUNDLE_PATH") ?? "resource://LantanaGroup.Link.Tests.BackendE2ETests.measures.NHSNAcuteCareHospitalMonthlyInitialPopulation.json";
            StartDate = Environment.GetEnvironmentVariable($"{prefix}_START_DATE") ?? "2023-01-01T00:00:00Z";
            EndDate = Environment.GetEnvironmentVariable($"{prefix}_END_DATE") ?? "2023-12-31T23:59:59Z";
            PatientIds = Environment.GetEnvironmentVariable($"{prefix}_PATIENT_IDS")?.Split(',')?.ToList() ?? defaultPatientIds ?? ["207727"];
            RemoveFacilityConfig = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_REMOVE_FACILITY_CONFIG") ?? "true");
            RemoveReport = Environment.GetEnvironmentVariable($"{prefix}_REMOVE_REPORT")?.ToLower() == "true";
            PollingIntervalSeconds = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_POLLING_INTERVAL_SECONDS") ?? defaultPollingIntervalSeconds.ToString());
            MaxRetryCount = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_MAX_RETRY_COUNT") ?? defaultMaxRetryCount.ToString());
            DownloadFileName = Environment.GetEnvironmentVariable($"{prefix}_DOWNLOAD_FILENAME") ?? $"{prefix.ToLower().Replace('_', '-')}-submission.zip";
            LokiScrapeWindowMinutes = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_LOKI_SCRAPE_WINDOW_MINUTES") ?? defaultLokiScrapeWindowMinutes.ToString());
        }

        public string MeasureBundleLocation { get; }
        public string StartDate { get; }
        public string EndDate { get; }
        public List<string> PatientIds { get; set; }
        public bool RemoveFacilityConfig { get; }
        public bool RemoveReport { get; }
        public int PollingIntervalSeconds { get; }
        public int MaxRetryCount { get; }
        public string DownloadFileName { get; }
        public int LokiScrapeWindowMinutes { get; }

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

    public class BasicAuthConfig(string prefix)
    {
        public bool ShouldAuthenticate { get; } = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_SHOULD_AUTHENTICATE") ?? "false");
        public string? Username { get; } = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_USERNAME");
        public string? Password { get; } = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_PASSWORD");
    }

    public class OAuthConfig(string prefix)
    {
        public bool ShouldAuthenticate { get; } = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SHOULD_AUTHENTICATE") ?? "false");
        public string? TokenEndpoint { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_TOKEN_ENDPOINT");
        public string? ClientId { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_ID");
        public string? ClientSecret { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_SECRET");
        public string Scope { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SCOPE") ?? "openid profile email";
        public string? Username { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_USERNAME");
        public string? Password { get; } = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_PASSWORD");
    }
}