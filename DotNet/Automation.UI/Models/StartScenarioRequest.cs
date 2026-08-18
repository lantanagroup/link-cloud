using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automation.UI.Models;

public class StartScenarioRequest : IValidatableObject
{
    public Guid? ScenarioId { get; set; }

    [Required]
    public AutomationScenarioKind Scenario { get; set; }

    /// <summary>
    /// How the report is triggered: Adhoc, ScheduledReport, or RegenerateReport.
    /// </summary>
    public ReportMethod ReportMethod { get; set; } = ReportMethod.Adhoc;

    // Zero is permitted: a scenario with imported patients only (no cohorts) generates
    // nothing and relies entirely on the imported-patient list. The run will fail
    // separately if neither generated nor imported patients are configured.
    [Range(0, 10000)]
    public int? PatientCount { get; set; }

    [Range(1, int.MaxValue)]
    public int? ResourcesPerPatient { get; set; }

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
    /// Measures selected for this run. When multiple measures are selected, the report
    /// is generated with all of them as report types, and qualifying patients must
    /// qualify for every selected measure.
    /// </summary>
    public List<ProfiledMeasureType> SelectedMeasures { get; set; } = [];

    public List<PatientCohortDefinition>? PatientCohorts { get; set; }

    /// <summary>
    /// Patients to fetch from the FHIR server by ID and include alongside the generated pool.
    /// </summary>
    public List<ImportedPatientInput>? ImportedPatientIds { get; set; }

    /// <summary>
    /// Patients supplied as FHIR transaction bundles (one bundle per patient).
    /// </summary>
    public List<ImportedPatientInput>? ImportedPatientBundles { get; set; }

    /// <summary>
    /// Reporting period start (UTC). When null, the system default is used.
    /// </summary>
    public DateTimeOffset? ReportPeriodStart { get; set; }

    /// <summary>
    /// Reporting period end (UTC). When null, the system default is used.
    /// </summary>
    public DateTimeOffset? ReportPeriodEnd { get; set; }

    /// <summary>
    /// Configured NHSN reporting Organization ID for this run.
    /// </summary>
    public string? NhsnOrganizationId { get; set; }

    /// <summary>
    /// Optional query plan template ID. When set, the run uses this template's
    /// query plan instead of the built-in defaults. When null, the system default is used.
    /// </summary>
    public Guid? QueryPlanTemplateId { get; set; }

    /// <summary>
    /// Optional normalization suite ID. When set, the run uses this suite's
    /// operations for normalization configuration. When null, the system default suite is used.
    /// </summary>
    public Guid? NormalizationSuiteId { get; set; }

    /// <summary>
    /// Optional organization-resource-map template ID. When set, the run uses this
    /// template's DataAcquisition organization-location mapping conditions.
    /// When null, the system default template is used.
    /// </summary>
    public Guid? OrganizationResourceMapTemplateId { get; set; }

    /// <summary>
    /// When true, a ScheduledReport run holds a short live window and accepts
    /// Admit/Discharge injections before finalizing the report.
    /// </summary>
    public bool IsLiveSimulation { get; set; }

    /// <summary>Live reporting window length in minutes (typically 5, 10, or 15).</summary>
    [Range(1, 60)]
    public int? ReportingWindowMinutes { get; set; }

    /// <summary>Optional number of generated patients to admit when the live window opens.</summary>
    [Range(0, 10000)]
    public int? SeedPatientCount { get; set; }

    /// <summary>
    /// Cross-field validation. Rejects inverted report windows
    /// (<see cref="ReportPeriodStart"/> &gt; <see cref="ReportPeriodEnd"/>) at the request
    /// boundary so that invalid windows are never forwarded to
    /// <c>StartScenarioRequestResolver</c> or downstream pipeline stages.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ReportPeriodStart.HasValue && ReportPeriodEnd.HasValue
            && ReportPeriodStart.Value > ReportPeriodEnd.Value)
        {
            yield return new ValidationResult(
                "ReportPeriodStart must be on or before ReportPeriodEnd.",
                new[] { nameof(ReportPeriodStart), nameof(ReportPeriodEnd) });
        }
    }

    public static StartScenarioRequest FromScenario(TestScenarioDefinition scenario) => new()
    {
        Scenario = AutomationScenarioKind.Custom,
        ScenarioName = scenario.Name,
        RunConfigurationJson = SerializeScenarioConfiguration(scenario),
        ReportMethod = scenario.ReportMethod,
        Seed = scenario.Seed,
        PatientCount = scenario.PatientCount,
        CleanupServiceData = scenario.CleanupServiceData,
        CleanupFhirData = scenario.CleanupFhirData,
        SelectedMeasures = scenario.SelectedMeasures,
        PatientCohorts = scenario.PatientCohorts,
        ImportedPatientIds = scenario.ImportedPatientIds,
        ImportedPatientBundles = scenario.ImportedPatientBundles,
        ReportPeriodStart = scenario.ReportPeriodStart,
        ReportPeriodEnd = scenario.ReportPeriodEnd,
        NhsnOrganizationId = scenario.NhsnOrganizationId,
        QueryPlanTemplateId = scenario.QueryPlanTemplateId,
        NormalizationSuiteId = scenario.NormalizationSuiteId,
        OrganizationResourceMapTemplateId = scenario.OrganizationResourceMapTemplateId,
        IsLiveSimulation = scenario.IsLiveSimulation,
        ReportingWindowMinutes = scenario.ReportingWindowMinutes,
        SeedPatientCount = scenario.SeedPatientCount,
    };

    private static string SerializeScenarioConfiguration(TestScenarioDefinition scenario)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(scenario, options);
    }
}
