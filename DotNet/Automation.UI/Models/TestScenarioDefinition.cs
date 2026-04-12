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

    /// <summary>Patient ID prefix string.</summary>
    public string PatientPrefix { get; set; } = "CustomPatient";

    // ----- Generation Mode -----

    /// <summary>
    /// When true, uses per-patient profiles (PatientProfiles) instead of random generation.
    /// PatientCount is ignored when profiles are provided.
    /// </summary>
    public bool UseMeasureEligibilityProfiles { get; set; }

    /// <summary>Per-patient eligibility profiles for measure-eligibility mode.</summary>
    public List<PatientProfile> PatientProfiles { get; set; } = [];

    /// <summary>
    /// Cohort-based configuration for measure-eligibility mode.
    /// Each cohort defines how many patients to generate with a given eligibility
    /// and which clinical scenario indices are allowed as the source pool.
    /// </summary>
    public List<PatientCohortDefinition> PatientCohorts { get; set; } = [];

    /// <summary>
    /// Stable clinical scenario IDs selected for inclusion.
    /// </summary>
    public List<string> SelectedClinicalScenarioIds { get; set; } = [];

    // ----- Discharge Control (Scheduled Report only) -----

    /// <summary>
    /// Number of patients to discharge during the report period.
    /// Only applicable when ReportMethod is ScheduledReport.
    /// For Standard mode, this is the total count of random patients that get discharged.
    /// </summary>
    public int DischargeCount { get; set; }

    /// <summary>
    /// When using measure-eligibility profiles: how many qualifying patients to discharge.
    /// </summary>
    public int DischargeQualifyingCount { get; set; }

    /// <summary>
    /// When using measure-eligibility profiles: how many non-qualifying patients to discharge.
    /// </summary>
    public int DischargeNonQualifyingCount { get; set; }

    // ----- Housekeeping -----

    /// <summary>
    /// Optional query plan template ID. When set, the scenario uses this template's
    /// queries instead of the built-in defaults from <c>QueryPlanDefaults</c>.
    /// When null, the system default query plan is used.
    /// </summary>
    public Guid? QueryPlanTemplateId { get; set; }

    /// <summary>
    /// Remove facility config, soft-delete reports, DA logs, and query dispatch config after the run.
    /// </summary>
    public bool CleanupServiceData { get; set; }

    /// <summary>
    /// Expunge all data from the FHIR server after the run.
    /// </summary>
    public bool CleanupFhirData { get; set; } = true;

    /// <summary>When the scenario was created or last updated.</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
