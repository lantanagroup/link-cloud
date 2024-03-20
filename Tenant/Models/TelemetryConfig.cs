namespace LantanaGroup.Link.Tenant.Models
{
    public class TelemetryConfig
    {
        public bool EnableRuntimeInstrumentation { get; set; } = false;
        public string TraceExporterEndpoint { get; set; } = string.Empty;
        public string MetricsEndpoint { get; set; } = string.Empty;
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;
    }
}
