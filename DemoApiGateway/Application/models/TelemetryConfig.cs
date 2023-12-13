namespace LantanaGroup.Link.DemoApiGateway.Application.models
{
    public class TelemetryConfig
    {
        public string TraceExporterEndpoint { get; set; } = string.Empty;
        public string MetricsEndpoint { get; set; } = string.Empty;
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;

    }
}
