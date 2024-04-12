using LantanaGroup.Link.Tenant.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Tenant.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Tenant Service";
        private static string ReportScheduledCounter = "report-scheduled-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> ReportScheduledObserved = meter.CreateCounter<int>(ReportScheduledCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            ReportScheduledObserved = meter.CreateCounter<int>(ReportScheduledCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }

    }
}
