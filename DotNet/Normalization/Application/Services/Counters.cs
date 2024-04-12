using LantanaGroup.Link.Normalization.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Normalization.Application.Services
{
    public class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Audit Service";
        private static string NormalizationCounter = "normalization-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> NormalizationObserved = meter.CreateCounter<int>(NormalizationCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            NormalizationObserved = meter.CreateCounter<int>(NormalizationCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
