namespace LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration
{
    /// <summary>
    /// Open Telemetry configuration options
    /// </summary>
    public class TelemetryConfig
    {
        /// <summary>
        /// Whether to enable tracing
        /// </summary>
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Whether to enable metrics
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Whether to enable runtime instrumentation
        /// </summary>
        public bool EnableRuntimeInstrumentation { get; set; } = false;

        /// <summary>
        /// The endpoint for the trace exporter
        /// </summary>
        public string TraceExporterEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// The endpoint for the metrics exporter
        /// </summary>
        public string MetricsEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// The endpoint for the telemetry collector
        /// </summary>
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;
    }
}
