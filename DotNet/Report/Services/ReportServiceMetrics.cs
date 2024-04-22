using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Report.Settings;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Report.Services
{
    public class ReportServiceMetrics : IReportServiceMetrics
    {
        public const string MeterName = $"Link.{ReportConstants.ServiceName}";

        public ReportServiceMetrics(IMeterFactory meterFactory)
        {
            Meter meter = meterFactory.Create(MeterName);
            ReportGeneratedCounter = meter.CreateCounter<long>("link_report_service.report_generated.count");
        }

        public Counter<long> ReportGeneratedCounter { get; private set; }
        public void IncrementReportGeneratedCounter(List<KeyValuePair<string, object?>> tags)
        {
            ReportGeneratedCounter.Add(1, tags.ToArray());
        }
    }
}
