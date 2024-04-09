using LantanaGroup.Link.Audit.Infrastructure.Telemetry;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditServiceMetrics
    {
        void IncrementAuditableEventCounter(List<KeyValuePair<string, object?>> tags);
        TrackedRequestDuration MeasureAuditSearchDuration();
    }
}
