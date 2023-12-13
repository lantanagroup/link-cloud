using LantanaGroup.Link.Audit.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Audit.Infrastructure.Telemetry
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Audit Service";
        private static string AuditableEventCounter = "auditable-event-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> AuditableEventObserved = meter.CreateCounter<int>(AuditableEventCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            AuditableEventObserved = meter.CreateCounter<int>(AuditableEventCounter);                
        }

        public static void Dispose()
        {
            meter.Dispose();
        }

    }
}
