using System.Runtime.Serialization;

namespace QueryDispatch.Application.Models
{
    [DataContract]
    public class TelemetryConfig
    {
        [DataMember]
        public string TraceExporterEndpoint { get; set; } = string.Empty;
        [DataMember]
        public string MetricsEndpoint { get; set; } = string.Empty;
        [DataMember]
        public string TelemetryCollectorEndpoint { get; set; } = string.Empty;
    }
}
