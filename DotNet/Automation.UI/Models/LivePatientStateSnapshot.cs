namespace Automation.UI.Models;

public sealed class LivePatientStateSnapshot
{
    public IReadOnlyList<string> Admitted { get; init; } = [];
    public IReadOnlyList<string> DischargedDuringWindow { get; init; } = [];
    /// <summary>Data/pattern report-inclusion baseline. Not the census union.</summary>
    public IReadOnlyList<string> ExpectedPopulation { get; init; } = [];
    public IReadOnlyList<LivePatientPoolEntry> Pool { get; init; } = [];
    public LivePatientPoolTotals PoolTotals { get; init; } = new();
    public bool AcceptingInjections { get; init; }
    public DateTimeOffset? WindowStartUtc { get; init; }
    public DateTimeOffset? WindowEndUtc { get; init; }
    public DateTimeOffset? ReportGenerationTimeUtc { get; init; }
}

public sealed class LiveSimulationDiagnostics
{
    public DateTimeOffset? WindowStartUtc { get; init; }
    public DateTimeOffset? WindowEndUtc { get; init; }
    public DateTimeOffset? ReportGenerationTimeUtc { get; init; }
    public List<PatientStateEvent> EventLog { get; init; } = [];
    public List<string> CurrentlyAdmitted { get; init; } = [];
    public List<string> DischargedDuringWindow { get; init; } = [];
    /// <summary>Data/pattern report-inclusion baseline. Not the census union.</summary>
    public List<string> ExpectedPopulation { get; init; } = [];
    public List<LivePatientPoolEntry> Pool { get; init; } = [];
    public LivePatientPoolTotals PoolTotals { get; init; } = new();
    public List<string> ActualPopulation { get; init; } = [];
    public bool? InclusionPassed { get; init; }
    public List<string> MissingFromReport { get; init; } = [];
    public List<string> UnexpectedInReport { get; init; } = [];
}
