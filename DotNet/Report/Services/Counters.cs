using LantanaGroup.Link.Report.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Report.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link Report Service";
        private static string MeasureReportCounter = "measure-report-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> MeasureReportObserved = meter.CreateCounter<int>(MeasureReportCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            MeasureReportObserved = meter.CreateCounter<int>(MeasureReportCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
