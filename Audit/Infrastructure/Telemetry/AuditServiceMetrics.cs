using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Audit.Infrastructure.Telemetry
{
    public class AuditServiceMetrics
    {
        public AuditServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create("LinkAuditService");
            AuditableEventCounter = meter.CreateCounter<int>("audit_event_count");
        }

        public Counter<int> AuditableEventCounter { get; private set; }
    }
}
