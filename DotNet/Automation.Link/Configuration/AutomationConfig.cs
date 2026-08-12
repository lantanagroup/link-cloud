namespace LantanaGroup.Link.Automation.Link.Configuration;

/// <summary>
/// Configuration for the Link automation library.
/// Consumers populate this from any source (environment variables, JSON, DI, etc.)
/// and pass it to the automation components.
/// </summary>
public class AutomationConfig
{
    /// <summary>
    /// FHIR server URL <b>Automation.UI itself</b> talks to for outbound calls
    /// (readiness probe, bundle uploads, generation pipeline, <c>$everything</c> reads,
    /// cleanup). Reflects whatever URL is reachable from wherever Automation.UI is hosted:
    /// <list type="bullet">
    ///   <item>When Automation.UI runs <i>inside</i> the docker compose network this is
    ///         the in-network DNS name (e.g. <c>http://fhir-server:8080/fhir</c>).</item>
    ///   <item>When Automation.UI runs <i>on the host</i> against a dockerized stack this
    ///         is the host-reachable URL via the published port mapping
    ///         (e.g. <c>http://localhost:6157/fhir</c>).</item>
    /// </list>
    /// In real deployments the URL comes from environment / Azure config; both fields
    /// typically resolve to the same value since there is only one FHIR server in play.
    /// </summary>
    public string FhirServerBase { get; set; } = "http://fhir-server:8080/fhir";

    /// <summary>
    /// FHIR server URL Automation registers on each test facility's
    /// <c>FhirQueryConfiguration</c> (via <c>FacilitySetupHelper</c>). This URL is
    /// persisted in the Tenant DB and later read back by <b>Link's own services</b>
    /// (DataAcquisition, Normalization, &hellip;) when they need to query the FHIR
    /// server. Their vantage point is <i>inside</i> the docker network, so this URL
    /// must be resolvable from there &mdash; in compose, that's always the service DNS
    /// name <c>http://fhir-server:8080/fhir</c>, regardless of where Automation.UI itself
    /// is hosted. There is only one physical FHIR server; this property exists separately
    /// from <see cref="FhirServerBase"/> only because the consumer's network vantage
    /// point can differ.
    /// </summary>
    public string FacilityFhirServerBase { get; set; } = "http://fhir-server:8080/fhir";

    public string LokiBaseUrl { get; set; } = string.Empty;
    public string LokiAppLabel { get; set; } = string.Empty;
    public string? DownloadPath { get; set; }

    public OAuthConfig FhirServerOAuth { get; set; } = new();
    public BasicAuthConfig FhirServerBasicAuth { get; set; } = new();

    public FhirQuerySettings FhirQuery { get; set; } = new();

    /// <summary>
    /// Link-specific FHIR bundle generation controls that are mapped into
    /// <c>LantanaGroup.Automation.Generation.FhirGenerationConfig</c> at runtime.
    /// </summary>
    public FhirGenerationSettings FhirGeneration { get; set; } = new();

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
        /// Maximum number of patients processed concurrently by the streaming
        /// generation/upload pipeline. Lower values reduce memory pressure.
        /// </summary>
        public int MaxConcurrentPatients { get; set; } = 2;

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

    public class KafkaConfig
    {
        public string RestProxyBaseUrl { get; set; } = string.Empty;
    }
}
