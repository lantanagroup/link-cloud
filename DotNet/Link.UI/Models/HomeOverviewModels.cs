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
    public DateTimeOffset LoadedAt { get; init; }
}

public sealed class FacilityCard
{
    public bool Reachable { get; init; }
    public string? Message { get; init; }
    public int Total { get; init; }
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
