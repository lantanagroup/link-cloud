using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using QueryDispatch.Application.Interfaces;
using QueryDispatch.Application.Settings;
using System.Diagnostics.Metrics;

namespace QueryDispatch.Application.Services
{
    public class QueryDispatchServiceMetrics : IQueryDispatchServiceMetrics
    {
        public const string MeterName = $"Link.{QueryDispatchConstants.ServiceName}";

        private readonly Counter<long> _patientsDispatched;
        private readonly Histogram<double> _dispatchDuration;

        public QueryDispatchServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            _patientsDispatched = meter.CreateCounter<long>("link_querydispatch_patients_dispatched_count");
            _dispatchDuration = meter.CreateHistogram<double>("link_querydispatch_dispatch_duration", "ms");
        }

        public void IncrementPatientsDispatched(string facilityId, string outcome)
        {
            _patientsDispatched.Add(1,
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.Outcome, outcome)
            ]);
        }

        public void RecordDispatchDuration(string facilityId, double durationMilliseconds)
        {
            _dispatchDuration.Record(durationMilliseconds,
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId)
            ]);
        }
    }
}
