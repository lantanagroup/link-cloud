namespace LantanaGroup.Link.Tenant.Models
{
    public class TelemetryConfig
    {
        public string TraceExporterEndpoint { get; set; } = string.Empty;
        public string MetricsEndpoint { get; set; } = string.Empty;
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;
    }
}
