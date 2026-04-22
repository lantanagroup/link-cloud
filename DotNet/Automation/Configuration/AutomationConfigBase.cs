namespace LantanaGroup.Automation.Configuration;

/// <summary>
/// Base configuration for the automation framework.
/// Platform-specific implementations extend this with additional settings.
/// </summary>
public class AutomationConfigBase
{
    public string? DownloadPath { get; set; }
    public bool CleanupTestData { get; set; } = true;

    public OAuthConfig DefaultOAuth { get; set; } = new();
    public BasicAuthConfig DefaultBasicAuth { get; set; } = new();

    public FhirServerConfigBase FhirServer { get; set; } = new();
    public DatabaseConfigBase Database { get; set; } = new();
    public LogScraperConfigBase LogScraper { get; set; } = new();
    public MessageBusConfigBase MessageBus { get; set; } = new();

    public class FhirServerConfigBase
    {
        public string ExternalBaseUrl { get; set; } = "http://localhost:8080/fhir";
        public string InternalBaseUrl { get; set; } = "http://localhost:8080/fhir";
        public OAuthConfig OAuth { get; set; } = new();
        public BasicAuthConfig BasicAuth { get; set; } = new();
    }

    public class DatabaseConfigBase
    {
        public string Server { get; set; } = "localhost";
        public string UserId { get; set; } = "sa";
        public string Password { get; set; } = "";
    }

    public class LogScraperConfigBase
    {
        public string BaseUrl { get; set; } = "http://localhost:3100";
    }

    public class MessageBusConfigBase
    {
        public string BootstrapServers { get; set; } = "localhost:9092";
        public string RestProxyBaseUrl { get; set; } = "http://localhost:8082";
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
