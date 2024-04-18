namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    public class TelemetrySettings
    {
        public bool EnableTelemetry { get; set; } = true;
        public bool EnableTracing { get; set; } = true;
        public bool EnableMetrics { get; set; } = true;
        public string? MeterName { get; set; }
        public bool EnableRuntimeInstrumentation { get; set; } = false;
        public bool EnableOtelCollector = true;
        public string? OtelCollectorEndpoint { get; set; }
        public bool EnableAzureMonitor { get; set; } = false;
        public string? AzureMonitorConnectionString { get; set; }
    }
}
