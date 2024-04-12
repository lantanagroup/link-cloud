using LantanaGroup.Link.Account.Domain.Entities;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Account.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Account Service";
        private static string AccountServiceCounter = "account-service-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> AccountServiceObserved = meter.CreateCounter<int>(AccountServiceCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            AccountServiceObserved = meter.CreateCounter<int>(AccountServiceCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
