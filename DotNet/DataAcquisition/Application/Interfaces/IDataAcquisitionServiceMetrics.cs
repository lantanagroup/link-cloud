using LantanaGroup.Link.Shared.Application.Services.Telemetry;

namespace LantanaGroup.Link.DataAcquisition.Application.Interfaces
{
    public interface IDataAcquisitionServiceMetrics
    {
        void IncrementResourceAcquiredCounter(List<KeyValuePair<string, object?>> tags);
        TrackedRequestDuration MeasureDataRequestDuration(List<KeyValuePair<string, object?>> tags);
    }
}
