namespace Link.UI.Models;

public sealed class ReportBatchLaunchPage
{
    public bool Ready { get; init; }

    public int SavedCap { get; init; }

    public int Copies { get; init; } = 1;

    public int MaxConcurrency { get; init; }

    public string? SelectedEhr { get; init; }

    public IReadOnlyList<Guid> SelectedScenarioIds { get; init; } = [];

    public IReadOnlyList<string> Errors { get; init; } = [];

    public IReadOnlyList<ReportBatchLaunchEhr> EhrSystems { get; init; } = [];

    public IReadOnlyList<ReportBatchLaunchScenario> Scenarios { get; init; } = [];
}

public sealed class ReportBatchLaunchEhr
{
    public string Name { get; init; } = string.Empty;

    public string FhirServerBase { get; init; } = string.Empty;

    public bool IsDefault { get; init; }

    public bool Ready { get; init; }

    public string? ReadinessError { get; init; }
}

public sealed class ReportBatchLaunchScenario
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Group { get; init; } = string.Empty;

    public string ReportMethodLabel { get; init; } = string.Empty;

    public bool IsSystemScenario { get; init; }
}
