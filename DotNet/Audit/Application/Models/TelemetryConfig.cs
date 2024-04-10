using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public class TelemetryConfig
    {
        public bool EnableTracing { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableRuntimeInstrumentation { get; set; } = false;
        public string TraceExporterEndpoint { get; set; } = string.Empty;
        public string MetricsEndpoint { get; set; } = string.Empty;
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;

    }
}
