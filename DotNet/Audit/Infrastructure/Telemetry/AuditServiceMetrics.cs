using LantanaGroup.Link.Audit.Application.Interfaces;
using LantanaGroup.Link.Audit.Settings;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Audit.Infrastructure.Telemetry
{
    public class AuditServiceMetrics : IAuditServiceMetrics
    {
        public const string MeterName = $"Link.{AuditConstants.ServiceName}";

        private readonly Histogram<double> _auditSearchDuration;
        private readonly TimeProvider _timeProvider;

        public AuditServiceMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;

            Meter meter = meterFactory.Create(MeterName);
            AuditableEventCounter = meter.CreateCounter<long>("link_audit_service.auditable_event.count");
            _auditSearchDuration = meter.CreateHistogram<double>("link_audit_service.audit.search.duration", "ms");
        }

        public Counter<long> AuditableEventCounter { get; private set; }

        public void IncrementAuditableEventCounter(List<KeyValuePair<string, object?>> tags)
        {
            AuditableEventCounter.Add(1, tags.ToArray());
        }

        public TrackedRequestDuration MeasureAuditSearchDuration(List<KeyValuePair<string, object?>> tags)
        {            
            return new TrackedRequestDuration(_auditSearchDuration, _timeProvider, tags);
        }        
    }
}
