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
            PatientIdentifiedCounter = meter.CreateCounter<long>("link_census_service.patient_identified.count");
        }

        public Counter<long> PatientIdentifiedCounter { get; private set; }
        public void IncrementPatientIdentifiedCounter(List<KeyValuePair<string, object?>> tags)
        {
            PatientIdentifiedCounter.Add(1, tags.ToArray());
        }
    }
}
