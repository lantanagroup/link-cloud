using PatientsToQuery.Application.Models;
using System.Diagnostics.Metrics;

namespace PatientsToQuery.Application.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link PatientToQuery Service";
        private static string PatientQueryCounter = "patient-query-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> PatientQueryObserved = meter.CreateCounter<int>(PatientQueryCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            PatientQueryObserved = meter.CreateCounter<int>(PatientQueryCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
