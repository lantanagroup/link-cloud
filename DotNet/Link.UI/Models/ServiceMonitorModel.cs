namespace Link.UI.Models;

public sealed class ServiceMonitorModel
{
    public ServiceMonitorModel(string pollUrl, bool compact = false, string? detail = null, string? healthUrl = null)
    {
        PollUrl = pollUrl;
        Compact = compact;
        HealthUrl = healthUrl ?? "";
        Detail = string.IsNullOrWhiteSpace(detail)
            ? "Click a service to pin CPU, RAM, and API p95."
            : detail;
    }

    public string PollUrl { get; }

    public bool Compact { get; }

    public string HealthUrl { get; }

    public string Detail { get; }
}
