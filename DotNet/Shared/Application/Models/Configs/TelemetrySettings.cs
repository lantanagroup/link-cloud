namespace LantanaGroup.Link.Shared.Application.Models.Configs
{
    /// <summary>
    /// Open Telemetry configuration options
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// Whether to enable telemetry
        /// </summary>
        public bool EnableTelemetry { get; set; } = true;

        /// <summary>
        /// Whether to enable tracing
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Whether to enable metrics
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// The meter name for the service registering telemetry
        /// </summary>
        public string? MeterName { get; set; }

        /// <summary>
        /// Whether to enable runtime instrumentation
        /// </summary>
        public bool EnableRuntimeInstrumentation { get; set; } = false;

        /// <summary>
        /// Whether to enable instrumentation of Entity Framework
        /// </summary>
        public bool InstrumentEntityFramework { get; set; } = false;

        /// <summary>
        /// Whether to export to the OTEL collector
        /// </summary>
        public bool EnableOtelCollector = true;

        /// <summary>
        /// The endpoint for the telemetry collector
        /// </summary>
        public string? OtelCollectorEndpoint { get; set; }

        /// <summary>
        /// Whether to export to Azure Monitor
        /// </summary>
        public bool EnableAzureMonitor { get; set; } = false;

        /// <summary>
        /// The connection string for Azure Monitor
        /// </summary>
        public string? AzureMonitorConnectionString { get; set; }
    }
}
