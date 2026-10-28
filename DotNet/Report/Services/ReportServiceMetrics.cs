using LantanaGroup.Link.Report.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using System.Diagnostics.Metrics;

namespace LantanaGroup.Link.Report.Services
{
    public class ReportServiceMetrics : IReportServiceMetrics
    {
        private readonly Counter<long> _statusTransitionCounter;
        private readonly Histogram<double> _persistDuration;

        public ReportServiceMetrics(IMeterFactory meterFactory, ServiceInformation serviceInformation)
        {
            Meter meter = meterFactory.Create($"Link.{serviceInformation.ServiceConfigName}");
            ReportGeneratedCounter = meter.CreateCounter<long>("link_report_service.report_generated.count");
            _statusTransitionCounter = meter.CreateCounter<long>(DiagnosticNames.ReportStatusTransitionCount);
            _persistDuration = meter.CreateHistogram<double>(DiagnosticNames.ReportPersistDuration, "ms");
        }

        public Counter<long> ReportGeneratedCounter { get; private set; }
        public void IncrementReportGeneratedCounter(List<KeyValuePair<string, object?>> tags)
        {
            ReportGeneratedCounter.Add(1, tags.ToArray());
        }

        public void IncrementStatusTransition(string facilityId, string from, string to)
        {
            _statusTransitionCounter.Add(1,
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId),
                new KeyValuePair<string, object?>(DiagnosticNames.From, from),
                new KeyValuePair<string, object?>(DiagnosticNames.To, to)
            ]);
        }

        public void RecordPersistDuration(string facilityId, double durationMilliseconds)
        {
            _persistDuration.Record(durationMilliseconds,
            [
                new KeyValuePair<string, object?>(DiagnosticNames.FacilityId, facilityId)
            ]);
        }
    }
}
