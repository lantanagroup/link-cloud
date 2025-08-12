using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Domain.Settings;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class DataAcquisitionServiceMetrics : IDataAcquisitionServiceMetrics
    {
        public const string MeterName = $"Link.{DataAcquisitionConstants.ServiceName}";

        private readonly Histogram<double> _dataRequestDuration;
        private readonly TimeProvider _timeProvider;

        public DataAcquisitionServiceMetrics(IMeterFactory meterFactory, TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;

            Meter meter = meterFactory.Create(MeterName);
            ResourceAcquiredCounter = meter.CreateCounter<long>("link_data_acquisition_service.resource_acquired.count");
            _dataRequestDuration = meter.CreateHistogram<double>("link_data_acquisition_service.data_request.duration", "ms");
        }

        public Counter<long> ResourceAcquiredCounter { get; private set; }
        public void IncrementResourceAcquiredCounter(List<KeyValuePair<string, object?>> tags)
        {
            ResourceAcquiredCounter.Add(1, tags.ToArray());
        }

        public TrackedRequestDuration MeasureDataRequestDuration(List<KeyValuePair<string, object?>> tags)
        {
            return new TrackedRequestDuration(_dataRequestDuration, _timeProvider, tags);
        }
    }
}
