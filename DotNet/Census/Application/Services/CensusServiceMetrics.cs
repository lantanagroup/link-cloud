using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Census.Application.Services
{
    public class CensusServiceMetrics : ICensusServiceMetrics
    {
        public const string MeterName = $"Link.{CensusConstants.ServiceName}";

        public CensusServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            PatientAdmittedCounter = meter.CreateCounter<long>("link_census_service.patient_admitted.count");
            PatientDischargedCounter = meter.CreateCounter<long>("link_census_service.patient_discharged.count");
        }

        public Counter<long> PatientAdmittedCounter { get; private set; }
        public void IncrementPatientAdmittedCounter(List<KeyValuePair<string, object?>> tags)
        {
            PatientAdmittedCounter.Add(1, tags.ToArray());
        }

        public Counter<long> PatientDischargedCounter { get; private set; }
        public void IncrementPatientDischargedCounter(List<KeyValuePair<string, object?>> tags)
        {
            PatientDischargedCounter.Add(1, tags.ToArray());
        }
    }
}
