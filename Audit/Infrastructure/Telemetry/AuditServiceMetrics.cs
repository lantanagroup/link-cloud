using System;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Audit.Infrastructure.Telemetry
{
    public class AuditServiceMetrics
    {
        public const string MeterName = "LinkAuditService";

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

        public TrackedRequestDuration MeasureAuditSearchDuration()
        {            
            return new TrackedRequestDuration(_auditSearchDuration, _timeProvider);
        }        
    }

    public class TrackedRequestDuration : IDisposable
    {
        private readonly TimeProvider _timeProvider;
        private readonly long _requestStartTime;
        private readonly Histogram<double> _histogram;

        public TrackedRequestDuration(Histogram<double> histogram, TimeProvider timeProvider)
        {
            _histogram = histogram;
            _timeProvider = timeProvider;
            _requestStartTime = timeProvider.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = _timeProvider.GetElapsedTime(_requestStartTime);
            _histogram.Record(elapsed.TotalMilliseconds);
        }
    }
}
