using LantanaGroup.Automation.Generation;
using LantanaGroup.Link.Automation.Link.Configuration;
using System.Reflection;

namespace LantanaGroup.Link.Tests.E2ETests;

/// <summary>
/// Environment variable-based configuration bridge for BackendE2ETests.
/// Creates <see cref="AutomationConfig"/> and <see cref="TestScenarioConfig"/> instances
/// populated from environment variables, preserving backward compatibility with
/// the Docker-compose test environment.
/// </summary>
public static class TestConfig
{
    // FHIR server
    public static string ExternalFhirServerBase => Environment.GetEnvironmentVariable("EXTERNAL_FHIR_SERVER_BASE_URL") ?? "http://localhost:6157/fhir";
    public static string InternalFhirServerBase => Environment.GetEnvironmentVariable("INTERNAL_FHIR_SERVER_BASE_URL") ?? "http://fhir-server:8080/fhir";

    // Service URLs — direct to each service (not through BFF)
    public static string TenantServiceBase => Environment.GetEnvironmentVariable("TENANT_SERVICE_BASE_URL") ?? "http://localhost:8074";
    public static string CensusServiceBase => Environment.GetEnvironmentVariable("CENSUS_SERVICE_BASE_URL") ?? "http://localhost:8064";
    public static string DataAcquisitionServiceBase => Environment.GetEnvironmentVariable("DATAACQUISITION_SERVICE_BASE_URL") ?? "http://localhost:8065";
    public static string NormalizationServiceBase => Environment.GetEnvironmentVariable("NORMALIZATION_SERVICE_BASE_URL") ?? "http://localhost:8068";
    public static string QueryDispatchServiceBase => Environment.GetEnvironmentVariable("QUERYDISPATCH_SERVICE_BASE_URL") ?? "http://localhost:8071";
    public static string ReportServiceBase => Environment.GetEnvironmentVariable("REPORT_SERVICE_BASE_URL") ?? "http://localhost:8072";
    public static string MeasureServiceBase => Environment.GetEnvironmentVariable("MEASURE_SERVICE_BASE_URL") ?? "http://localhost:8067";
    public static string ValidationServiceBase => Environment.GetEnvironmentVariable("VALIDATION_SERVICE_BASE_URL") ?? "http://localhost:8075";
    public static string SubmissionServiceBase => Environment.GetEnvironmentVariable("SUBMISSION_SERVICE_BASE_URL") ?? "http://localhost:8073";

    // AdminBFF — only for operations that have no direct service endpoint
    public static string AdminBffBase => Environment.GetEnvironmentVariable("ADMIN_BFF_BASE_URL") ?? "http://localhost:8063/api";

    // Infrastructure
    public static string LokiBaseUrl => Environment.GetEnvironmentVariable("LOKI_BASE_URL") ?? "http://localhost:3100";
    public static string? SmokeTestDownloadPath =>
    Environment.GetEnvironmentVariable("SMOKE_TEST_DOWNLOAD_PATH");
    public static bool CleanupSmokeTestData => bool.Parse(Environment.GetEnvironmentVariable("CLEANUP_SMOKE_TEST_DATA") ?? "true");

    /// <summary>
    /// Builds an <see cref="AutomationConfig"/> from environment variables.
    /// This is the primary configuration used by tests in this project.
    /// </summary>
    public static AutomationConfig BuildAutomationConfig() => new()
    {
        ExternalFhirServerBase = ExternalFhirServerBase,
        InternalFhirServerBase = InternalFhirServerBase,
        AdminBffBase = AdminBffBase,
        LokiBaseUrl = LokiBaseUrl,
        DownloadPath = SmokeTestDownloadPath,
        CleanupTestData = CleanupSmokeTestData,
        AdminBffOAuth = BuildOAuthConfig("ADMINBFF"),
        FhirServerOAuth = BuildOAuthConfig("FHIRSERVER"),
        FhirServerBasicAuth = BuildBasicAuthConfig("FHIRSERVER"),
        FhirQuery = new AutomationConfig.FhirQuerySettings
        {
            MaxConcurrentRequests = 8,
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null,
            TimeZone = null
        },
        Database = new AutomationConfig.DatabaseConfig
        {
            Server = Environment.GetEnvironmentVariable("E2E_SQL_SERVER") ?? "localhost,1433",
            UserId = Environment.GetEnvironmentVariable("E2E_SQL_USER") ?? "sa",
            Password = Environment.GetEnvironmentVariable("E2E_SQL_PASSWORD") ?? string.Empty
        },
        Kafka = new AutomationConfig.KafkaConfig
        {
            BootstrapServers = Environment.GetEnvironmentVariable("E2E_KAFKA_BOOTSTRAP_SERVERS") ?? "localhost:9094",
            RestProxyBaseUrl = Environment.GetEnvironmentVariable("E2E_KAFKA_REST_PROXY_URL") ?? "http://localhost:8082",
            User = Environment.GetEnvironmentVariable("E2E_KAFKA_USER") ?? string.Empty,
            Password = Environment.GetEnvironmentVariable("E2E_KAFKA_PASSWORD") ?? string.Empty
        }
    };

    public static TestScenarioConfig AdhocReportingSmokeTestConfig => BuildScenarioConfig("ADHOC_REPORTING_SMOKE_TEST",
        defaultPatientIds: []);

    public static TestScenarioConfig MegaPatientTestConfig => BuildScenarioConfig("MEGA_PATIENT_TEST",
        defaultPatientIds: [],
        defaultPollingIntervalSeconds: 5,
        defaultMaxPollingDurationMinutes: 25,
        defaultLokiScrapeWindowMinutes: 20);

    public static TestScenarioConfig BuildScenarioConfig(
        string prefix,
        List<string>? defaultPatientIds = null,
        int defaultPollingIntervalSeconds = 3,
        int defaultMaxPollingDurationMinutes = 3,
        int defaultLokiScrapeWindowMinutes = 5)
    {
        return new TestScenarioConfig
        {
            MeasureBundleLocation = Environment.GetEnvironmentVariable($"{prefix}_MEASURE_BUNDLE_PATH")
                ?? ProfiledMeasureCatalog.GetBundleLocation(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation),
            StartDate = Environment.GetEnvironmentVariable($"{prefix}_START_DATE") ?? "2023-01-01T00:00:00Z",
            EndDate = Environment.GetEnvironmentVariable($"{prefix}_END_DATE") ?? "2023-12-31T23:59:59Z",
            PatientIds = Environment.GetEnvironmentVariable($"{prefix}_PATIENT_IDS")?.Split(',')?.ToList() ?? defaultPatientIds ?? ["207727"],
            RemoveFacilityConfig = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_REMOVE_FACILITY_CONFIG") ?? "false"),
            RemoveReport = Environment.GetEnvironmentVariable($"{prefix}_REMOVE_REPORT")?.ToLower() == "true",
            PollingIntervalSeconds = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_POLLING_INTERVAL_SECONDS") ?? defaultPollingIntervalSeconds.ToString()),
            MaxPollingDurationMinutes = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_MAX_POLLING_DURATION_MINUTES") ?? defaultMaxPollingDurationMinutes.ToString()),
            DownloadFileName = Environment.GetEnvironmentVariable($"{prefix}_DOWNLOAD_FILENAME") ?? $"{prefix.ToLower().Replace('_', '-')}-submission.zip",
            LokiScrapeWindowMinutes = int.Parse(Environment.GetEnvironmentVariable($"{prefix}_LOKI_SCRAPE_WINDOW_MINUTES") ?? defaultLokiScrapeWindowMinutes.ToString())
        };
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

    private static OAuthConfig BuildOAuthConfig(string prefix) => new()
    {
        ShouldAuthenticate = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SHOULD_AUTHENTICATE") ?? "false"),
        TokenEndpoint = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_TOKEN_ENDPOINT"),
        ClientId = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_ID"),
        ClientSecret = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_CLIENT_SECRET"),
        Scope = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_SCOPE") ?? "openid profile email",
        Username = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_USERNAME"),
        Password = Environment.GetEnvironmentVariable($"{prefix}_OAUTH_PASSWORD")
    };

    private static BasicAuthConfig BuildBasicAuthConfig(string prefix) => new()
    {
        ShouldAuthenticate = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_SHOULD_AUTHENTICATE") ?? "false"),
        Username = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_USERNAME"),
        Password = Environment.GetEnvironmentVariable($"{prefix}_BASICAUTH_PASSWORD")
    };
}
