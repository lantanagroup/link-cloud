using LantanaGroup.Link.Account.Domain.Entities;
using LantanaGroup.Link.Account.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.Account.Infrastructure
{
    public class ServiceActivitySource
    {
        public static string Version = string.Empty;
        public static string ServiceName = AccountConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, Version);

        public static void Initialize(string version)
        {
            Version = version;
            Instance = new ActivitySource(ServiceName, Version);
        }
    }
}
