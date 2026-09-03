using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;
using LantanaGroup.Link.Terminology.Application.Interfaces;
using LantanaGroup.Link.Terminology.Application.Settings;

namespace LantanaGroup.Link.Terminology.Application.Services;

public class TerminologyServiceMetrics : ITerminologyServiceMetrics
{
    public const string MeterName = $"Link.{TerminologyConstants.ServiceName}";

    private readonly Counter<long> _lookupCount;
    private readonly Histogram<double> _lookupDuration;

    public TerminologyServiceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);
        _lookupCount = meter.CreateCounter<long>(DiagnosticNames.TerminologyLookupCount);
        _lookupDuration = meter.CreateHistogram<double>(DiagnosticNames.TerminologyLookupDuration, "ms");
    }

    public void IncrementLookupCount(string outcome, string groupKind)
    {
        _lookupCount.Add(1,
        [
            new KeyValuePair<string, object?>(DiagnosticNames.Outcome, outcome),
            new KeyValuePair<string, object?>(DiagnosticNames.GroupKind, groupKind)
        ]);
    }

    public void RecordLookupDuration(double durationMilliseconds, string groupKind, string cache)
    {
        _lookupDuration.Record(durationMilliseconds,
        [
            new KeyValuePair<string, object?>(DiagnosticNames.GroupKind, groupKind),
            new KeyValuePair<string, object?>(DiagnosticNames.Cache, cache)
        ]);
    }
}
