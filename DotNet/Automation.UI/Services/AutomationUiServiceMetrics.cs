using System.Diagnostics.Metrics;
using LantanaGroup.Link.Shared.Application.Models.Telemetry;

namespace Automation.UI.Services;

public sealed class AutomationUiServiceMetrics : IAutomationUiMetrics
{
    public const string MeterName = "Link.AutomationUI";

    private readonly Counter<long> _pollerHttp;

    public AutomationUiServiceMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _pollerHttp = meter.CreateCounter<long>(DiagnosticNames.AutomationPollerHttpCount);
    }

    public void IncrementPollerHttp(string domain, string outcome)
    {
        _pollerHttp.Add(1,
        [
            new KeyValuePair<string, object?>(DiagnosticNames.Domain, domain),
            new KeyValuePair<string, object?>(DiagnosticNames.Outcome, outcome)
        ]);
    }
}
