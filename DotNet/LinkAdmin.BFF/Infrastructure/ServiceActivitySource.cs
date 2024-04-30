using LantanaGroup.Link.LinkAdmin.BFF.Application.Models.Configuration;
using LantanaGroup.Link.LinkAdmin.BFF.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure
{
    public static class ServiceActivitySource
    {
        public static string Version = string.Empty;
        public static string ServiceName = LinkAdminConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, Version);

        public static void Initialize(string version)
        {
            Version = version;
            Instance = new ActivitySource(ServiceName, Version);
        }
    }
}
