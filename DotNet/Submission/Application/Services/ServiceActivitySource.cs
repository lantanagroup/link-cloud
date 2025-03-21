using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Submission.Settings;
using System.Diagnostics;

namespace LantanaGroup.Link.Submission.Application.Services
{
    public static class ServiceActivitySource
    {
        private static string _version = string.Empty;
        public static string ServiceName = SubmissionConstants.ServiceName;
        public static ActivitySource Instance { get; private set; } = new ActivitySource(ServiceName, _version);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _version = serviceInfo.Version;
            Instance = new ActivitySource(ServiceName, _version);
        }
    }
}
