using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Report.Services
{
    public class ReportServiceMetrics : IReportServiceMetrics
    {
        public ReportServiceMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            Meter meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            ReportGeneratedCounter = meter.CreateCounter<long>("link_report_service.report_generated.count");
        }

        public Counter<long> ReportGeneratedCounter { get; private set; }
        public void IncrementReportGeneratedCounter(List<KeyValuePair<string, object?>> tags)
        {
            ReportGeneratedCounter.Add(1, tags.ToArray());
        }
    }
}
