namespace LantanaGroup.Link.Automation.Link.Configuration;

/// <summary>
/// Configuration for the Link automation library.
/// Consumers populate this from any source (environment variables, JSON, DI, etc.)
/// and pass it to the automation components.
/// </summary>
public class AutomationConfig
{
    /// <summary>
    /// Base URL of the FHIR server Automation talks to. The exact same URL is also
    /// registered on each test facility's query config (via <c>FacilitySetupHelper</c>)
    /// so Link's services hit the same server &mdash; there is only ever one FHIR
    /// server in play. In a Docker-compose run this is the in-network name
    /// (e.g. <c>http://fhir-server:8080/fhir</c>); in dev-from-host it's whatever
    /// host-reachable URL points at the same instance
    /// (e.g. <c>http://host.docker.internal:6157/fhir</c> so DataAcquisition inside
    /// the container can resolve it too).
    /// </summary>
    public string FhirServerBase { get; set; } = "http://localhost:6157/fhir";
    public string AdminBffBase { get; set; } = "http://localhost:8063/api";
    public string LokiBaseUrl { get; set; } = "http://localhost:3100";
    public string? DownloadPath { get; set; }

    public OAuthConfig AdminBffOAuth { get; set; } = new();
    public OAuthConfig FhirServerOAuth { get; set; } = new();
    public BasicAuthConfig FhirServerBasicAuth { get; set; } = new();

    public FhirQuerySettings FhirQuery { get; set; } = new();

    /// <summary>
    /// Link-specific FHIR bundle generation controls that are mapped into
    /// <c>LantanaGroup.Automation.Generation.FhirGenerationConfig</c> at runtime.
    /// </summary>
    public FhirGenerationSettings FhirGeneration { get; set; } = new();

    public DatabaseConfig Database { get; set; } = new();

    public KafkaConfig Kafka { get; set; } = new();

    public class FhirQuerySettings
    {
        public int MaxConcurrentRequests { get; set; } = 8;
        public TimeSpan? MinAcquisitionPullTime { get; set; }
        public TimeSpan? MaxAcquisitionPullTime { get; set; }
        public string? TimeZone { get; set; }
    }

    public class FhirGenerationSettings
    {
        /// <summary>
        /// Controls low-value optional cross-resource references in generated FHIR
        /// (e.g., Provenance.target, ImagingStudy.basedOn, MedicationAdministration.request).
        /// </summary>
        public bool IncludeLowValueOptionalReferences { get; set; } = false;

        /// <summary>
        /// Resource distribution optimized for Link processing pipelines.
        /// Contains only the resource types Link consumes.
        /// </summary>
        public Dictionary<string, double> ResourceDistribution { get; set; } = new()
        {
            ["Observation"] = 0.30,
            ["Condition"] = 0.10,
            ["Procedure"] = 0.08,
            ["MedicationRequest"] = 0.08,
            ["MedicationAdministration"] = 0.10,
            ["DiagnosticReport"] = 0.07,
            ["ServiceRequest"] = 0.08,
            ["Coverage"] = 0.02,
            ["Specimen"] = 0.07,
        };
    }

    public class DatabaseConfig
    {
        public string Server { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class KafkaConfig
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string RestProxyBaseUrl { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
