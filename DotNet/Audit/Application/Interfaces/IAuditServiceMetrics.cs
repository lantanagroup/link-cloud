using LantanaGroup.Link.Shared.Application.Services.Telemetry;

namespace LantanaGroup.Link.Audit.Application.Interfaces
{
    public interface IAuditServiceMetrics
    {
        void IncrementAuditableEventCounter(List<KeyValuePair<string, object?>> tags);
        TrackedRequestDuration MeasureAuditSearchDuration(List<KeyValuePair<string, object?>> tags);
    }
}
