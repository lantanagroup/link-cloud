using LantanaGroup.Link.Validation.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Validation.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Validation Service";
        private static string ValidationCounter = "validation-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> ValidationObserved = meter.CreateCounter<int>(ValidationCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            ValidationObserved = meter.CreateCounter<int>(ValidationCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
