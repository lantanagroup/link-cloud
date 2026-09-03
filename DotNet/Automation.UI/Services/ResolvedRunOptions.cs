using Automation.UI.Models;
using LantanaGroup.Automation.Generation;

namespace Automation.UI.Services;

/// <summary>
/// Fully-resolved per-run options after merging request overrides with
/// scenario-kind defaults and saved-scenario JSON. Built by
/// <see cref="StartScenarioRequestResolver"/> and consumed by
/// <see cref="AutomationRunManager"/> at run time.
/// </summary>
public record ResolvedRunOptions(
    int PatientCount,
    int ResourcesPerPatient,
    int Seed,
    int PollingIntervalSeconds,
    int MaxPollingDurationMinutes,
    int LokiScrapeWindowMinutes,
    bool CleanupServiceData,
    bool CleanupFhirData,
    List<ProfiledMeasureType> SelectedMeasures,
    List<PatientProfile> PatientProfiles,
    List<PatientCohortDefinition> PatientCohorts,
    ReportMethod ReportMethod = ReportMethod.Adhoc,
    Guid? QueryPlanTemplateId = null,
    Guid? NormalizationSuiteId = null,
    Guid? OrganizationResourceMapTemplateId = null)
{
    /// <summary>
    /// Imported patients (referenced by ID, fetched from FHIR server at run time).
    /// </summary>
    public List<ImportedPatientInput> ImportedPatientIds { get; init; } = [];

    /// <summary>
    /// Imported patients (supplied as FHIR transaction bundle JSON, uploaded at run time).
    /// </summary>
    public List<ImportedPatientInput> ImportedPatientBundles { get; init; } = [];

    /// <summary>Reporting period start (UTC). Null = use system default.</summary>
    public DateTimeOffset? ReportPeriodStart { get; init; }

    /// <summary>Reporting period end (UTC). Null = use system default.</summary>
    public DateTimeOffset? ReportPeriodEnd { get; init; }

    /// <summary>NHSN reporting Organization ID for this run.</summary>
    public string NhsnOrganizationId { get; init; } = string.Empty;

    /// <summary>
    /// When true, the run holds a short live reporting window and accepts
    /// Admit/Discharge injections before finalizing the scheduled report.
    /// </summary>
    public bool IsLiveSimulation { get; init; }

    /// <summary>Live reporting window length in minutes (typically 5, 10, or 15).</summary>
    public int ReportingWindowMinutes { get; init; } = 10;

    public bool IsMetricsRun { get; init; }
    public string? BenchmarkKey { get; init; }
    public int? TargetDurationSeconds { get; init; }
    public int? Concurrency { get; init; }
    public bool FailRunOnBenchmark { get; init; }

    /// <summary>Measure template ids selected for this run (empty = use system templates for SelectedMeasures).</summary>
    public List<Guid> SelectedMeasureIds { get; init; } = [];

    /// <summary>
    /// FHIR measure-bundle JSON, one per selected template, in load order.
    /// System templates are seeded from <see cref="ProfiledMeasureCatalog.ReadBundleJson"/>.
    /// </summary>
    public List<string> MeasureBundleJsons { get; init; } = [];
}
