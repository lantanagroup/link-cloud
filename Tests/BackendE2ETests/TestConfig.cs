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
    // FHIR server. Two URLs reflect two vantage points on the same physical server:
    // FhirServerBase is what the test process itself reaches; FacilityFhirServerBase
    // is registered on each facility's FhirQueryConfiguration so Link's services
    // (DataAcquisition, Normalization, ...) running inside docker can reach the same
    // instance from their own network. In a fully-containerized run both collapse to
    // the in-network DNS name; in dev-from-host they differ.
    public static string FhirServerBase => Environment.GetEnvironmentVariable("FHIR_SERVER_BASE_URL")
        ?? Environment.GetEnvironmentVariable("EXTERNAL_FHIR_SERVER_BASE_URL") // legacy name
        ?? "http://localhost:6157/fhir";

    public static string FacilityFhirServerBase => Environment.GetEnvironmentVariable("FACILITY_FHIR_SERVER_BASE_URL")
        ?? Environment.GetEnvironmentVariable("INTERNAL_FHIR_SERVER_BASE_URL") // legacy name
        ?? "http://fhir-server:8080/fhir";

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

    // Automation.UI — used by AutomationUiApiSmokeTest to exercise the /api/runs endpoints.
    // Host port 5256 matches the docker-compose mapping (5256:5257).
    public static string AutomationUiBase => Environment.GetEnvironmentVariable("AUTOMATION_UI_BASE_URL") ?? "http://localhost:5256";

    // Infrastructure
    public static string LokiBaseUrl => Environment.GetEnvironmentVariable("Loki__Url") ?? "http://localhost:3100";
    public static string? AdhocReportTestDownloadPath =>
        Environment.GetEnvironmentVariable("ADHOC_REPORT_TEST_DOWNLOAD_PATH");
    public static bool CleanupAdhocReportTestData => bool.Parse(
        Environment.GetEnvironmentVariable("CLEANUP_ADHOC_REPORT_TEST_DATA") ?? "true");

    /// <summary>
    /// Builds an <see cref="AutomationConfig"/> from environment variables.
    /// This is the primary configuration used by tests in this project.
    /// </summary>
    public static AutomationConfig BuildAutomationConfig() => new()
    {
        FhirServerBase = FhirServerBase,
        FacilityFhirServerBase = FacilityFhirServerBase,
        LokiBaseUrl = LokiBaseUrl,
        DownloadPath = AdhocReportTestDownloadPath,
        FhirServerOAuth = BuildOAuthConfig("FHIRSERVER"),
        FhirServerBasicAuth = BuildBasicAuthConfig("FHIRSERVER"),
        FhirQuery = new AutomationConfig.FhirQuerySettings
        {
            MaxConcurrentRequests = 8,
            MinAcquisitionPullTime = null,
            MaxAcquisitionPullTime = null,
            TimeZone = null
        },
        Kafka = new AutomationConfig.KafkaConfig
        {
            RestProxyBaseUrl = Environment.GetEnvironmentVariable("Automation__Kafka__RestProxyBaseUrl")
                ?? Environment.GetEnvironmentVariable("E2E_KAFKA_REST_PROXY_URL")
                ?? "http://localhost:8082"
        }
    };

    public static TestScenarioConfig AdhocReportTestConfig => BuildScenarioConfig("ADHOC_REPORT_TEST",
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
            MeasureBundleJsons =
            [
                ProfiledMeasureCatalog.ReadBundleJson(ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation)
            ],
            StartDate = Environment.GetEnvironmentVariable($"{prefix}_START_DATE") ?? "2023-01-01T00:00:00Z",
            EndDate = Environment.GetEnvironmentVariable($"{prefix}_END_DATE") ?? "2023-12-31T23:59:59Z",
            PatientIds = Environment.GetEnvironmentVariable($"{prefix}_PATIENT_IDS")?.Split(',')?.ToList() ?? defaultPatientIds ?? ["207727"],
            CleanupServiceData = bool.Parse(Environment.GetEnvironmentVariable($"{prefix}_CLEANUP_SERVICE_DATA") ?? "false"),
            CleanupFhirData = CleanupAdhocReportTestData,
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
