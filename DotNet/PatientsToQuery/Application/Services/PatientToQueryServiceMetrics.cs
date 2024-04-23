using PatientsToQuery.Application.Interfaces;
using PatientsToQuery.Settings;
using System.Diagnostics.Metrics;

namespace PatientsToQuery.Application.Services
{
    public class PatientToQueryServiceMetrics : IPatientToQueryServiceMetrics
    {
        public const string MeterName = $"Link.{PatientsToQueryConstants.ServiceName}";

        public PatientToQueryServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
        }
    }
}
