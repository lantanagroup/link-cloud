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
    Guid? NormalizationSuiteId = null)
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
}
