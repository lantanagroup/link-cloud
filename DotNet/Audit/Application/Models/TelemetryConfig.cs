using System.Diagnostics;

namespace LantanaGroup.Link.Audit.Application.Models
{
    public class TelemetryConfig
    {
        public bool Enabled { get; set; } = true;
        public bool EnableTracing { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public bool EnableRuntimeInstrumentation { get; set; } = false;
        public bool EnableOtelCollector = true;
        public string? OtelCollectorEndpoint { get; set; }
        public bool EnableAzureMonitor { get; set; } = false;
        public string? AzureMonitorConnectionString { get; set; }
    }    
}
