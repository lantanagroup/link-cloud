using System.Text.Json.Serialization;

namespace Link.UI.Models;

/// <summary>
/// Slim home overview. Counts and the top few rows only. Each card stands on its own
/// when its source cannot be reached.
/// </summary>
public sealed class HomeOverviewModel
{
    public FacilityCard Facilities { get; init; } = new();
    public ReportCard Reports { get; init; } = new();
    public HealthCard Health { get; init; } = new();
    public RunCard Runs { get; init; } = new();
    public LogCard Logs { get; init; } = new();
    public ActivityCard Activity { get; init; } = new();
    public ServicePulseCard Pulse { get; init; } = new();
    public IReadOnlyList<HomeIssue> Issues { get; init; } = [];

    /// <summary>When false the overview has no automation section, links, or counts.</summary>
    public bool AutomationVisible { get; init; } = true;

    /// <summary>Tenant scope for real facilities. Null until classification supplies one.</summary>
    public string? RealScope { get; init; }

    public DateTimeOffset LoadedAt { get; init; }
}

public sealed class FacilityCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public int Total { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Automation { get; init; }

    public int? Regular { get; init; }

    public string TotalText => Reachable ? Total.ToString() : "—";
    public string AutomationText => Reachable && Automation is int value ? value.ToString() : "—";
    public string RegularText => Reachable && Regular is int value ? value.ToString() : "—";
}

public sealed class ReportCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public long Total { get; init; }
    public IReadOnlyList<HomeReportLine> Rows { get; init; } = [];

    public string TotalText => Reachable ? Total.ToString() : "—";
}

public sealed class HomeReportLine
{
    public string Id { get; init; } = "";
    public string FacilityId { get; init; } = "";
    public string Status { get; init; } = "";
    public string Badge { get; init; } = "";
    public string When { get; init; } = "";
    public string? AutomationRunId { get; init; }
}

public sealed class HealthCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public int Healthy { get; init; }
    public int Unhealthy { get; init; }
    public IReadOnlyList<string> UnhealthyNames { get; init; } = [];
    public bool ApiHealthReachable { get; init; }
    public string ApiHealthText { get; init; } = "";

    public string UnhealthyText => Reachable ? Unhealthy.ToString() : "—";
    public int HiddenUnhealthy => Math.Max(0, Unhealthy - UnhealthyNames.Count);
}

public sealed class RunCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public int ActiveCount { get; init; }
    public IReadOnlyList<HomeRunLine> Active { get; init; } = [];
    public IReadOnlyList<HomeRunLine> Recent { get; init; } = [];

    public string ActiveText => Reachable ? ActiveCount.ToString() : "—";
}

public sealed class HomeRunLine
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string Badge { get; init; } = "";
    public string When { get; init; } = "";
}

public sealed class LogCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public long Total { get; init; }
    public IReadOnlyList<HomeLogLine> Rows { get; init; } = [];
    public string? AuditMessage { get; init; }

    public string TotalText => Reachable ? Total.ToString() : "—";
}

public sealed class HomeLogLine
{
    public long Id { get; init; }
    public string FacilityId { get; init; } = "";
    public string Status { get; init; } = "";
    public string Badge { get; init; } = "";
    public string When { get; init; } = "";
    public string? AutomationRunId { get; init; }
}

/// <summary>Status totals and a seven-day created series. Counts only.</summary>
public sealed class ActivityCard
{
    public bool InFlightReachable { get; init; }
    public long InFlight { get; init; }
    public DateTimeOffset? OldestInFlightUtc { get; init; }
    public bool SubmittedReachable { get; init; }
    public long Submitted { get; init; }
    public bool CompletedReachable { get; init; }
    public long CompletedNotSubmitted { get; init; }
    public IReadOnlyList<TrendDay> Trend { get; init; } = [];

    public string InFlightText => InFlightReachable ? InFlight.ToString() : "—";
    public string SubmittedText => SubmittedReachable ? Submitted.ToString() : "—";
    public string CompletedText => CompletedReachable ? CompletedNotSubmitted.ToString() : "—";
}

public sealed class TrendDay
{
    public string Day { get; init; } = "";
    public bool Reachable { get; init; }
    public long Count { get; init; }

    public string CountText => Reachable ? Count.ToString() : "—";
}

/// <summary>Pipeline CPU and API latency from the same utilization read the metrics page uses.</summary>
public sealed class ServicePulseCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<ServiceChip> Chips { get; init; } = [];
}

public sealed class ServiceChip
{
    public string Name { get; init; } = "";
    public double? CpuPercent { get; init; }
    public double? ApiP95Ms { get; init; }
}

public readonly record struct PulseSample(string Name, string Group, double? CpuPercent, double? ApiP95Ms);

public sealed class HomeIssue
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Href { get; init; } = "";
}

/// <summary>Active and newest run rows for the home page. Not the 14-day chart query.</summary>
public sealed class HomeRunSlice
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public int ActiveCount { get; init; }
    public IReadOnlyList<AutomationRunRow> Active { get; init; } = [];
    public IReadOnlyList<AutomationRunRow> Recent { get; init; } = [];

    public static HomeRunSlice Unavailable(string message) => new() { Message = message };
}
