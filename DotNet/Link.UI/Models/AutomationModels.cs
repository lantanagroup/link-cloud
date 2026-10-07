namespace Link.UI.Models;

public sealed class AutomationRunQuery
{
    public int PageNumber { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }
}

public sealed class AutomationRunRow
{
    public Guid RunId { get; init; }

    public string RunName { get; init; } = string.Empty;

    public string Scenario { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public int PatientCount { get; init; }

    public int Seed { get; init; }

    public bool IsMetricsRun { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }

    public string? Error { get; init; }

    public string? Duration { get; init; }

    public string? FacilityId { get; init; }

    public bool AutomationCreatedFacility { get; init; }

    public string? ReportId { get; init; }

    public int? TemplateVersion { get; init; }

    public bool InProgress { get; init; }

    public bool ThrowawayFacility { get; init; }
}

public sealed class AutomationDayBucket
{
    public string Date { get; init; } = string.Empty;

    public int Succeeded { get; init; }

    public int Failed { get; init; }

    public int Cancelled { get; init; }

    public int Other { get; init; }
}

public sealed class AutomationStats
{
    public int TotalRuns { get; init; }

    public int Succeeded { get; init; }

    public int Failed { get; init; }

    public int Cancelled { get; init; }

    public int Running { get; init; }

    public int Queued { get; init; }

    public double AvgDurationSeconds { get; init; }

    public double SuccessRate { get; init; }

    public IReadOnlyList<AutomationDayBucket> RunsPerDay { get; init; } = [];
}

public sealed class AutomationDashboardPage
{
    public bool StorageConfigured { get; init; }

    public bool StorageReachable { get; init; }

    public string? Message { get; init; }

    public bool LiveConfigured { get; init; }

    public AutomationStats Stats { get; init; } = new();

    public IReadOnlyList<AutomationRunRow> ActiveRuns { get; init; } = [];

    public IReadOnlyList<AutomationRunRow> RecentRuns { get; init; } = [];

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public long TotalCount { get; init; }

    public int TotalPages { get; init; } = 1;

    public string SortBy { get; init; } = "createdAt";

    public string SortDir { get; init; } = "desc";
}

public sealed class AutomationScenarioChoice
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsSystemScenario { get; init; }
}

public sealed class AutomationScenarioList
{
    public bool StorageConfigured { get; init; }

    public bool StorageReachable { get; init; }

    public string? Message { get; init; }

    public bool Truncated { get; init; }

    public IReadOnlyList<AutomationScenarioChoice> Scenarios { get; init; } = [];
}

public sealed class AutomationStartChoice
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

public sealed class AutomationNewRunPage
{
    public bool EngineReady { get; init; }

    public string? Message { get; init; }

    public string? Error { get; init; }

    public string? Choice { get; init; }

    public string? RunName { get; init; }

    public bool ScenariosTruncated { get; init; }

    public IReadOnlyList<AutomationStartChoice> BuiltIn { get; init; } = [];

    public IReadOnlyList<AutomationScenarioChoice> Scenarios { get; init; } = [];
}

public sealed class AutomationRunPage
{
    public bool Found { get; init; }

    public bool StorageConfigured { get; init; }

    public bool StorageReachable { get; init; }

    public string? Message { get; init; }

    public bool LiveConfigured { get; init; }

    public Guid RequestedId { get; init; }

    public AutomationRunRow? Run { get; init; }
}
