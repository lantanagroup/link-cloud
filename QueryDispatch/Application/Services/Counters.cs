using QueryDispatch.Application.Models;
using System.Diagnostics.Metrics;

namespace QueryDispatch.Application.Services
{
    public class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link QueryDispatch Service";
        private static string QueryDispatchCounter = "query-dispatch-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> QueryDispatchObserved = meter.CreateCounter<int>(QueryDispatchCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            QueryDispatchObserved = meter.CreateCounter<int>(QueryDispatchCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
