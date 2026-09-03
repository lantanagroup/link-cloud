using System.Diagnostics.Metrics;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Shared.Application.Services.Telemetry;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Services
{
    public class DataAcquisitionServiceMetrics : IDataAcquisitionServiceMetrics
    {
        private readonly Histogram<double> _dataRequestDuration;
        private readonly Histogram<double> _semaphoreWaitDuration;
        private readonly Counter<long> _resourceAcquiredCounter;
        private readonly TimeProvider _timeProvider;

        public DataAcquisitionServiceMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, ServiceInformation serviceInformation)
        {
            _timeProvider = timeProvider;

            // Use the configured service name for the meter name to ensure it matches OpenTelemetry registration
            Meter meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            _resourceAcquiredCounter = meter.CreateCounter<long>(DiagnosticNames.DataAcquisitionResourceAcquiredCount);
            _dataRequestDuration = meter.CreateHistogram<double>(DiagnosticNames.DataAcquisitionQueryDuration, "ms");
            _semaphoreWaitDuration = meter.CreateHistogram<double>(DiagnosticNames.DataAcquisitionSemaphoreWaitDuration, "ms");
        }

        public void IncrementResourceAcquiredCounter(List<KeyValuePair<string, object?>> tags)
        {
            _resourceAcquiredCounter.Add(1, tags.ToArray());
        }

        public TrackedRequestDuration MeasureDataRequestDuration(List<KeyValuePair<string, object?>> tags)
        {
            return new TrackedRequestDuration(_dataRequestDuration, _timeProvider, tags);
        }

        public void RecordSemaphoreWaitDuration(string facilityId, double durationMilliseconds)
        {
            _semaphoreWaitDuration.Record(durationMilliseconds,
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId)
            ]);
        }
    }
}
