using LantanaGroup.Link.DataAcquisition.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.DataAcquisition.Application.Services
{
    public class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link DataAcquisition Service";
        private static string DataAcquiredCounter = "data-acquired-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> DataAcquiredObserved = meter.CreateCounter<int>(DataAcquiredCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            DataAcquiredObserved = meter.CreateCounter<int>(DataAcquiredCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
