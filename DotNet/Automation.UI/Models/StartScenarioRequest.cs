using LantanaGroup.Automation.Generation;
using System.ComponentModel.DataAnnotations;

namespace Automation.UI.Models;

public class StartScenarioRequest
{
    [Required]
    public AutomationScenarioKind Scenario { get; set; }

    /// <summary>
    /// How the report is triggered: Adhoc, ScheduledReport, or RegenerateReport.
    /// </summary>
    public ReportMethod ReportMethod { get; set; } = ReportMethod.Adhoc;

    [Range(1, 10000)]
    public int? PatientCount { get; set; }

    [Range(1, int.MaxValue)]
    public int? ResourcesPerPatient { get; set; }

    [StringLength(64)]
    public string? PatientPrefix { get; set; }

    [Range(1, int.MaxValue)]
    public int? Seed { get; set; }

    [StringLength(120)]
    public string? ScenarioName { get; set; }

    public string? RunConfigurationJson { get; set; }

    /// <summary>
    /// Remove facility config, soft-delete reports, DA logs, and query dispatch config after the run.
    /// </summary>
    public bool? CleanupServiceData { get; set; }

    /// <summary>
    /// Expunge all data from the FHIR server after the run.
    /// </summary>
    public bool? CleanupFhirData { get; set; }

    /// <summary>
    /// Single measure for backward compatibility. When set and <see cref="SelectedMeasures"/>
    /// is empty, this measure is used.
    /// </summary>
    public ProfiledMeasureType? SelectedMeasure { get; set; }

    /// <summary>
    /// Measures selected for this run. When multiple measures are selected, the report
    /// is generated with all of them as report types, and qualifying patients must
    /// qualify for every selected measure.
    /// </summary>
    public List<ProfiledMeasureType> SelectedMeasures { get; set; } = [];

    /// <summary>
    /// Per-patient eligibility profiles for measure-eligibility generation mode.
    /// When provided, the Custom scenario uses profile-driven pipeline generation instead
    /// of the standard <c>Generate()</c> code path. Each entry controls whether that
    /// patient qualifies for the measure's Initial Population.
    /// When null or empty, standard random generation is used.
    /// </summary>
    public List<PatientProfile>? PatientProfiles { get; set; }

    public List<PatientCohortDefinition>? PatientCohorts { get; set; }

    /// <summary>
    /// Optional query plan template ID. When set, the run uses this template's
    /// query plan instead of the built-in defaults. When null, the system default is used.
    /// </summary>
    public Guid? QueryPlanTemplateId { get; set; }
}
