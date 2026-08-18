using LantanaGroup.Automation.Generation;

namespace Automation.UI.Models;

/// <summary>
/// A named, saveable test scenario configuration.
/// Stored in MongoDB and presented in the UI for quick reuse.
/// </summary>
public class TestScenarioDefinition
{
    /// <summary>Unique identifier (Guid stored as string in Mongo).</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-facing name for this scenario.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional description / notes.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// When true, the scenario was seeded by the system and cannot be modified or deleted.
    /// It can still be cloned.
    /// </summary>
    public bool IsSystemScenario { get; set; }

    // ----- Report -----

    /// <summary>How the report is triggered.</summary>
    public ReportMethod ReportMethod { get; set; } = ReportMethod.Adhoc;

    /// <summary>Selected measures for the run.</summary>
    public List<ProfiledMeasureType> SelectedMeasures { get; set; } = [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];

    // ----- Patient Pool -----

    /// <summary>Generation seed for deterministic output.</summary>
    public int Seed { get; set; } = 20260329;

    /// <summary>Number of patients (used in Standard generation mode).</summary>
    public int PatientCount { get; set; } = 10;

    /// <summary>Minimum resources per patient (inclusive). Generation picks a random value in [Min, Max] per patient.</summary>
    public int ResourcesPerPatientMin { get; set; } = 250;

    /// <summary>Maximum resources per patient (inclusive). When equal to Min, every patient gets the same count.</summary>
    public int ResourcesPerPatientMax { get; set; } = 250;

    // ----- Generation Mode -----

    /// <summary>
    /// Cohort-based configuration for measure-eligibility mode.
    /// Each cohort defines how many patients to generate with a given eligibility
    /// and which clinical scenario indices are allowed as the source pool.
    /// </summary>
    public List<PatientCohortDefinition> PatientCohorts { get; set; } = [];

    /// <summary>
    /// Configured NHSN reporting Organization ID for this scenario.
    /// </summary>
    public string NhsnOrganizationId { get; set; } = string.Empty;

    // ----- Housekeeping -----

    /// <summary>
    /// Optional query plan template ID. When set, the scenario uses this template's
    /// queries instead of the built-in defaults from <c>QueryPlanDefaults</c>.
    /// When null, the system default query plan is used.
    /// </summary>
    public Guid? QueryPlanTemplateId { get; set; }

    /// <summary>
    /// Optional normalization suite ID. When set, the scenario uses this suite's
    /// operations instead of creating a simple default normalization.
    /// When null, the system default normalization suite is used.
    /// </summary>
    public Guid? NormalizationSuiteId { get; set; }

    /// <summary>
    /// Optional organization-resource-map template ID used to configure
    /// DataAcquisition organization-location mapping behavior for the run.
    /// When null, the system default template is used.
    /// </summary>
    public Guid? OrganizationResourceMapTemplateId { get; set; }

    /// <summary>
    /// Remove facility config, soft-delete reports, DA logs, and query dispatch config after the run.
    /// </summary>
    public bool CleanupServiceData { get; set; }

    /// <summary>
    /// Expunge all data from the FHIR server after the run.
    /// </summary>
    public bool CleanupFhirData { get; set; } = true;

    // ----- Reporting Period -----

    /// <summary>
    /// Reporting period start (UTC). When null, the system default
    /// (<c>2023-01-01T00:00:00Z</c>) is used. The UI auto-suggests a value that
    /// encompasses imported-patient encounter dates when imports are added.
    /// </summary>
    public DateTimeOffset? ReportPeriodStart { get; set; }

    /// <summary>
    /// Reporting period end (UTC). When null, the system default
    /// (<c>2023-12-31T23:59:59Z</c>) is used.
    /// </summary>
    public DateTimeOffset? ReportPeriodEnd { get; set; }

    // ----- Live Scheduled Simulation -----

    /// <summary>
    /// When true, a ScheduledReport run holds a short live window and accepts
    /// Admit/Discharge injections before finalizing the report.
    /// </summary>
    public bool IsLiveSimulation { get; set; }

    /// <summary>
    /// Live reporting window length in minutes (typically 5, 10, or 15).
    /// </summary>
    public int ReportingWindowMinutes { get; set; } = 10;

    // ----- Imported Patients (supplemental, separate from cohorts/random pool) -----

    /// <summary>
    /// Patients to fetch from the FHIR server by ID and include alongside the generated pool.
    /// These patients are assumed to already exist on the server; their data is fetched at run
    /// time, classified, and added to the manifest. They are NOT uploaded and NOT expunged on
    /// cleanup.
    /// </summary>
    public List<ImportedPatientInput> ImportedPatientIds { get; set; } = [];

    /// <summary>
    /// Patients supplied as FHIR transaction bundles (one bundle per patient). These are
    /// uploaded to the FHIR server during the run, included in the manifest, and expunged on
    /// cleanup like any generated patient.
    /// </summary>
    public List<ImportedPatientInput> ImportedPatientBundles { get; set; } = [];

    /// <summary>When the scenario was created or last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
