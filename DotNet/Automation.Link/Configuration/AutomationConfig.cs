namespace LantanaGroup.Link.Automation.Link.Configuration;

/// <summary>
/// Configuration for the Link automation library.
/// Consumers populate this from any source (environment variables, JSON, DI, etc.)
/// and pass it to the automation components.
/// </summary>
public class AutomationConfig
{
    public string ExternalFhirServerBase { get; set; } = "http://localhost:6157/fhir";
    public string InternalFhirServerBase { get; set; } = "http://fhir-server:8080/fhir";
    public string AdminBffBase { get; set; } = "http://localhost:8063/api";
    public string LokiBaseUrl { get; set; } = "http://localhost:3100";
    public string? DownloadPath { get; set; }
    public bool CleanupTestData { get; set; } = true;

    public OAuthConfig AdminBffOAuth { get; set; } = new();
    public OAuthConfig FhirServerOAuth { get; set; } = new();
    public BasicAuthConfig FhirServerBasicAuth { get; set; } = new();

    public FhirQuerySettings FhirQuery { get; set; } = new();

    public DatabaseConfig Database { get; set; } = new();

    public KafkaConfig Kafka { get; set; } = new();

    public class FhirQuerySettings
    {
        public int MaxConcurrentRequests { get; set; } = 8;
        public TimeSpan MinAcquisitionPullTime { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan MaxAcquisitionPullTime { get; set; } = TimeSpan.FromHours(24);
        public string TimeZone { get; set; } = "America/New_York";
    }

    public class DatabaseConfig
    {
        public string Server { get; set; } = "localhost,1433";
        public string UserId { get; set; } = "sa";
        public string Password { get; set; } = "7h3I^xMY%cgO";
    }

    public class KafkaConfig
    {
        public string BootstrapServers { get; set; } = "localhost:9094";
        public string RestProxyBaseUrl { get; set; } = "http://localhost:8082";
        public string User { get; set; } = "user";
        public string Password { get; set; } = "password";
    }
}
