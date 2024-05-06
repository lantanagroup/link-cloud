using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Infrastructure
{
    public class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = AccountConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
