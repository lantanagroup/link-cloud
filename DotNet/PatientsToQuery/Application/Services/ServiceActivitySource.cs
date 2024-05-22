using PatientsToQuery.Application.Models;
using PatientsToQuery.Settings;
using System.Diagnostics;
using LantanaGroup.Link.Shared.Application.Models;

namespace PatientsToQuery.Application.Services
{
    public class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = PatientsToQueryConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
