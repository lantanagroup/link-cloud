using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

public enum LivePatientOrigin
{
    Cohort,
    Import,
    Generated,
    Upload,
    FhirId
}

public enum LivePatientCensusState
{
    NotAdmitted,
    Admitted,
    DischargedDuringWindow
}

public sealed class LivePatientPoolEntry
{
    public string PatientId { get; init; } = "";
    public LivePatientOrigin Origin { get; init; }
    public ScheduledInpatientPattern? Pattern { get; init; }
    public LivePatientCensusState CensusState { get; init; }
    public bool ExpectedInReport { get; init; }
}

public sealed class LivePatientSeed
{
    public string PatientId { get; init; } = "";
    public LivePatientOrigin Origin { get; init; } = LivePatientOrigin.Cohort;
    public ScheduledInpatientPattern? Pattern { get; init; }

    /// <summary>
    /// Data/pattern report-inclusion baseline. When null, the tracker falls back to
    /// <see cref="ScheduledInpatientPatternExtensions.GetCensusBehavior"/> for
    /// <c>ExpectedInReport</c>. Census Admit/Discharge must not change this.
    /// </summary>
    public bool? ExpectedInReport { get; init; }
}

public static class LiveEventSources
{
    public const string Pattern = "Pattern";
    public const string Auto = "Auto";
    public const string UI = "UI";
    public const string API = "API";
    public const string Generated = "Generated";
    public const string Upload = "Upload";
    public const string FhirId = "FhirId";
    public const string Seed = "Seed";
}

public sealed class LivePatientPoolTotals
{
    public int Total { get; init; }
    public int NotAdmitted { get; init; }
    public int Admitted { get; init; }
    public int DischargedDuringWindow { get; init; }
}
