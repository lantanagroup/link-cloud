using LantanaGroup.Link.Shared.Application.Services.Telemetry;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces
{
    public interface IDataAcquisitionServiceMetrics
    {
        void IncrementResourceAcquiredCounter(List<KeyValuePair<string, object?>> tags);
        TrackedRequestDuration MeasureDataRequestDuration(List<KeyValuePair<string, object?>> tags);
        void RecordSemaphoreWaitDuration(string facilityId, double durationMilliseconds);
        /// <summary>
        /// Counts one request that arrived on the deprecated /api/data prefix.
        /// </summary>
        /// <param name="route">The matched route template, or "unmatched". Never a raw path.</param>
        /// <param name="method">The HTTP method, already limited to a fixed set of values.</param>
        void IncrementPathRewriteCounter(string route, string method);
    }
}
