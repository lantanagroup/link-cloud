using LantanaGroup.Link.MeasureEval.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.MeasureEval.Services
{
    public static class Counters
    {
        private static string _version = string.Empty;
        private static string _serviceName = "Link MeasureEval Service";
        private static string MeasureEvaluationCounter = "measure-evaluation-observed";

        public static Meter meter = new Meter(_serviceName, _version);
        public static Counter<int> MeasureEvaluationObserved = meter.CreateCounter<int>(MeasureEvaluationCounter);

        public static void Initialize(ServiceInformation serviceInfo)
        {
            _serviceName = serviceInfo.Name;
            _version = serviceInfo.Version;

            //re-initialize meter
            Dispose();
            meter = new Meter(_serviceName, _version);
            MeasureEvaluationObserved = meter.CreateCounter<int>(MeasureEvaluationCounter);
        }

        public static void Dispose()
        {
            meter.Dispose();
        }
    }
}
